using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Auth;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity + JWT + Refresh Tokens (PRD §5).</summary>
public class IdentityService(
    UserManager<AppUser> userManager,
    IJwtTokenService jwtTokenService,
    AppDbContext db,
    IConfiguration configuration) : IIdentityService, ISubscriptionActivator
{
    // Cliente estático para validar o ID token junto ao Google (uma chamada por login).
    private static readonly HttpClient http = new();

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuário não encontrado.");
        return ToDto(user);
    }

    /// <summary>
    /// Cadastro unificado: só identidade (nome, e-mail, senha). O perfil
    /// (artista ou contratante) é escolhido e preenchido na etapa seguinte.
    /// O papel vem como Contractor provisório até a conclusão do perfil.
    /// </summary>
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var user = await CreateUserAsync(request.Email, request.Password, request.Name, UserRole.Contractor);
        return await GenerateTokensAsync(user);
    }

    /// <summary>
    /// Login/cadastro com Google: valida o ID token (assinatura/aud/exp via tokeninfo),
    /// autentica quem já tem conta com o e-mail ou cria a conta na hora (perfil
    /// pendente de conclusão, igual ao cadastro unificado).
    /// </summary>
    public async Task<AuthResponse> GoogleLoginAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new ValidationException("Token do Google ausente.");

        var resposta = await http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");
        if (!resposta.IsSuccessStatusCode)
            throw new UnauthorizedException("Token do Google inválido ou expirado.");

        using var documento = System.Text.Json.JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        var raiz = documento.RootElement;

        var audience = configuration["Google:ClientId"];
        if (!string.Equals(raiz.GetProperty("aud").GetString(), audience, StringComparison.Ordinal))
            throw new UnauthorizedException("Token do Google não pertence a este aplicativo.");

        var email = raiz.GetProperty("email").GetString();
        var verificado = raiz.GetProperty("email_verified").GetString();
        if (string.IsNullOrEmpty(email) || !string.Equals(verificado, "true", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedException("A conta Google não tem e-mail verificado.");

        var existente = await userManager.FindByEmailAsync(email);
        if (existente is not null)
            return await GenerateTokensAsync(existente);

        var nome = raiz.TryGetProperty("name", out var nomeEl) && !string.IsNullOrWhiteSpace(nomeEl.GetString())
            ? nomeEl.GetString()!
            : email.Split('@')[0];

        // Senha aleatória (login sempre pelo Google); e-mail confirmado pelo Google.
        var user = await CreateUserAsync(email, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)) + "aA1", nome, UserRole.Contractor);
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        return await GenerateTokensAsync(user);
    }

    /// <summary>
    /// Conclusão do perfil pós-registro: define Artista ou Contratante, cria o
    /// perfil com os dados informados (incluindo CPF/CEP) e reemite o token com
    /// o papel definitivo.
    /// </summary>
    public async Task<AuthResponse> CompleteProfileAsync(Guid userId, CompleteProfileRequest request)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuário não encontrado.");
        if (user.ArtistId is not null || user.ContractorId is not null)
            throw new BusinessRuleException("Seu perfil já foi concluído.");

        var tipo = (request.Tipo ?? string.Empty).Trim();
        if (tipo is not ("Artista" or "Contratante"))
            throw new ValidationException("Tipo inválido. Use Artista ou Contratante.");

        if (tipo == "Artista")
        {
            var nomeArtistico = (request.ArtisticName ?? string.Empty).Trim();
            if (nomeArtistico.Length < 2)
                throw new ValidationException("Informe o nome artístico.");
            if (request.CategoryId is null || request.CategoryId == Guid.Empty)
                throw new ValidationException("Selecione a categoria do artista.");
            if (!await db.ArtistCategories.AnyAsync(cat => cat.Id == request.CategoryId && cat.IsActive))
                throw new ValidationException("Categoria inválida.");

            if (!Enum.TryParse<ArtistType>(request.ArtistType, ignoreCase: true, out var artistType))
                artistType = ArtistType.Solo;

            // Troca definitiva do papel (o token reemitido ao final já traz Artist).
            await userManager.RemoveFromRoleAsync(user, UserRole.Contractor.ToString());
            await userManager.AddToRoleAsync(user, UserRole.Artist.ToString());
            user.Role = UserRole.Artist;

            var artist = new Artist
            {
                UserId = user.Id,
                Type = artistType,
                Name = user.DisplayName,
                ArtisticName = nomeArtistico,
                CategoryId = request.CategoryId.Value,
                City = request.City?.Trim() ?? string.Empty,
                State = request.State?.Trim() ?? string.Empty,
                Phone = request.Phone?.Trim() ?? string.Empty,
                Cpf = request.Cpf?.Trim(),
                Cep = request.Cep?.Trim()
            };
            db.Artists.Add(artist);

            var slug = ArtistService.Slugify(nomeArtistico);
            if (await db.ArtistProfiles.AnyAsync(p => p.Slug == slug))
                slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";

            List<string> Clean(List<string>? values) => (values ?? [])
                .Select(v => v.Trim())
                .Where(v => v.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            db.ArtistProfiles.Add(new ArtistProfile
            {
                ArtistId = artist.Id,
                Slug = slug,
                Styles = Clean(request.Styles),
                EventTypes = Clean(request.EventTypes)
            });

            await db.SaveChangesAsync();
            user.ArtistId = artist.Id;
            await userManager.UpdateAsync(user);
            return await GenerateTokensAsync(user);
        }

        // Contratante
        var contractor = new Contractor
        {
            UserId = user.Id,
            Type = ContractorType.Person,
            Name = user.DisplayName,
            CompanyName = request.CompanyName?.Trim() ?? string.Empty,
            Phone = request.Phone?.Trim() ?? string.Empty,
            City = request.City?.Trim() ?? string.Empty,
            State = request.State?.Trim() ?? string.Empty,
            Cpf = request.Cpf?.Trim(),
            Cep = request.Cep?.Trim()
        };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync();
        user.ContractorId = contractor.Id;
        await userManager.UpdateAsync(user);

        await VincularShowsPendentesAsync(contractor.Id, contractor.Phone);
        return await GenerateTokensAsync(user);
    }

    public async Task<AuthResponse> RegisterArtistAsync(RegisterArtistRequest request)
    {
        if (!Enum.TryParse<ArtistType>(request.ArtistType, ignoreCase: true, out var artistType))
            throw new ValidationException("Tipo de artista inválido. Use Solo ou Band.");

        var user = await CreateUserAsync(request.Email, request.Password, request.ArtisticName, UserRole.Artist);

        var categoryExists = await db.ArtistCategories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive);
        if (!categoryExists)
            throw new ValidationException("Categoria inválida.");

        var artist = new Artist
        {
            UserId = user.Id,
            Type = artistType,
            Name = request.Name.Trim(),
            ArtisticName = request.ArtisticName.Trim(),
            CategoryId = request.CategoryId,
            City = request.City.Trim(),
            State = request.State.Trim(),
            Phone = request.Phone.Trim()
        };
        db.Artists.Add(artist);

        // Slug provisório; regenerado no onboarding quando o nome artístico mudar.
        var slug = ArtistService.Slugify(request.ArtisticName);
        var slugTaken = await db.ArtistProfiles.AnyAsync(p => p.Slug == slug);
        if (slugTaken)
            slug = $"{slug}-{Guid.NewGuid().ToString("N")[..6]}";

        // Estilos e tipos de evento vêm do wizard de registro (multibox por categoria).
        List<string> Clean(List<string>? values) => (values ?? [])
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        db.ArtistProfiles.Add(new ArtistProfile
        {
            ArtistId = artist.Id,
            Slug = slug,
            Styles = Clean(request.Styles),
            EventTypes = Clean(request.EventTypes)
        });

        await db.SaveChangesAsync();

        user.ArtistId = artist.Id;
        await userManager.UpdateAsync(user);

        return await GenerateTokensAsync(user);
    }

    public async Task<AuthResponse> RegisterContractorAsync(RegisterContractorRequest request)
    {
        if (!Enum.TryParse<ContractorType>(request.ContractorType, ignoreCase: true, out var contractorType))
            contractorType = ContractorType.Person;

        var user = await CreateUserAsync(request.Email, request.Password, request.Name, UserRole.Contractor);

        var contractor = new Contractor
        {
            UserId = user.Id,
            Type = contractorType,
            Name = request.Name.Trim(),
            CompanyName = request.CompanyName?.Trim() ?? string.Empty,
            Phone = request.Phone ?? string.Empty
        };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync();

        user.ContractorId = contractor.Id;
        await userManager.UpdateAsync(user);

        await VincularShowsPendentesAsync(contractor.Id, contractor.Phone);
        return await GenerateTokensAsync(user);
    }

    /// <summary>
    /// Fecha o ciclo do fluxo de visitante: solicitações "Aguardando conta" feitas com
    /// este telefone são vinculadas ao novo cadastro e confirmadas automaticamente.
    /// </summary>
    private async Task VincularShowsPendentesAsync(Guid contractorId, string? phone)
    {
        var phoneDigits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        if (phoneDigits.Length < 10)
            return;

        var pending = await db.BookingRequests
            .Where(b => b.Status == BookingStatus.AwaitingAccount && b.GuestPhone == phoneDigits)
            .ToListAsync();

        var eventIds = pending.Where(b => b.EventId is not null).Select(b => b.EventId!.Value).ToList();
        var events = eventIds.Count > 0
            ? await db.Events.Where(e => eventIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id)
            : [];

        foreach (var bookingRequest in pending)
        {
            bookingRequest.ContractorId = contractorId;
            bookingRequest.Status = BookingStatus.Confirmed;
            bookingRequest.UpdatedAt = DateTime.UtcNow;

            if (bookingRequest.EventId is not null && events.TryGetValue(bookingRequest.EventId.Value, out var ev)
                && ev.Status == EventStatus.PreReserved)
            {
                ev.Status = EventStatus.Confirmed;
                ev.UpdatedAt = DateTime.UtcNow;
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedException("E-mail ou senha inválidos.");

        return await GenerateTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken)
            ?? throw new UnauthorizedException("Refresh token inválido.");
        if (!stored.IsActive)
            throw new UnauthorizedException("Refresh token expirado ou revogado.");

        var user = await userManager.FindByIdAsync(stored.UserId.ToString())
            ?? throw new UnauthorizedException("Usuário não encontrado.");

        // Rotação do refresh token
        stored.RevokedAt = DateTime.UtcNow;
        var (newRefresh, expiresAt) = jwtTokenService.GenerateRefreshToken();
        stored.ReplacedByToken = newRefresh;
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefresh,
            ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync();

        var (accessToken, accessExpires) = jwtTokenService.GenerateAccessToken(user);
        return new AuthResponse(accessToken, newRefresh, accessExpires, ToDto(user));
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (stored is not null)
        {
            stored.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    /// <summary>MVP sem e-mail: em Development o token é retornado ao cliente; em produção, nulo (iria por e-mail).</summary>
    public async Task<string?> ForgotPasswordAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return null; // não revela existência da conta

        return await userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new NotFoundException("Usuário não encontrado.");
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));
    }

    private async Task<AppUser> CreateUserAsync(string email, string password, string displayName, UserRole role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new BusinessRuleException("Já existe uma conta com este e-mail.");

        var user = new AppUser
        {
            UserName = email.Trim().ToLowerInvariant(),
            Email = email.Trim().ToLowerInvariant(),
            DisplayName = displayName.Trim(),
            Role = role,
            SubscriptionTier = SubscriptionTier.Gratuito
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new ValidationException(string.Join(" ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role.ToString());
        return user;
    }

    private async Task<AuthResponse> GenerateTokensAsync(AppUser user)
    {
        var (accessToken, accessExpires) = jwtTokenService.GenerateAccessToken(user);
        var (refreshToken, refreshExpires) = jwtTokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = refreshExpires
        });
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, accessExpires, ToDto(user));
    }

    private static UserDto ToDto(AppUser user)
    {
        var perfilCompleto = user.Role switch
        {
            UserRole.Admin => true,
            UserRole.Artist => user.ArtistId is not null,
            UserRole.Contractor => user.ContractorId is not null,
            _ => false
        };
        return new UserDto(user.Id, user.Email ?? string.Empty, user.DisplayName, user.Role, user.ArtistId,
            user.ContractorId, user.SubscriptionTier, perfilCompleto);
    }

    /// <summary>Promoção da conta para Assinante após PAYMENT_CONFIRMED no webhook.</summary>
    public async Task<bool> ActivateAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuário não encontrado.");
        if (user.SubscriptionTier == SubscriptionTier.Assinante)
            return false;

        user.SubscriptionTier = SubscriptionTier.Assinante;
        await userManager.UpdateAsync(user);
        return true;
    }
}
