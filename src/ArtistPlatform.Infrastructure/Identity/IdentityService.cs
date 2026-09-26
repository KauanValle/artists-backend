using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Auth;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Infrastructure.Identity;

/// <summary>ASP.NET Core Identity + JWT + Refresh Tokens (PRD §5).</summary>
public class IdentityService(
    UserManager<AppUser> userManager,
    IJwtTokenService jwtTokenService,
    AppDbContext db) : IIdentityService
{
    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuário não encontrado.");
        return ToDto(user);
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
        db.ArtistProfiles.Add(new ArtistProfile { ArtistId = artist.Id, Slug = slug });

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
            Phone = request.Phone ?? string.Empty
        };
        db.Contractors.Add(contractor);
        await db.SaveChangesAsync();

        user.ContractorId = contractor.Id;
        await userManager.UpdateAsync(user);

        return await GenerateTokensAsync(user);
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
            Role = role
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

    private static UserDto ToDto(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName, user.Role, user.ArtistId, user.ContractorId);
}
