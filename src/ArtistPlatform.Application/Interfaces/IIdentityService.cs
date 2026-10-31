using ArtistPlatform.Application.DTOs;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>Autenticação via ASP.NET Core Identity + JWT (implementada na Infrastructure).</summary>
public interface IIdentityService
{
    Task<UserDto> GetCurrentUserAsync(Guid userId);
    /// <summary>Cadastro unificado (identidade apenas); perfil completado depois.</summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    /// <summary>Conclusão do perfil pós-registro (artista ou contratante) — reemite o token.</summary>
    Task<AuthResponse> CompleteProfileAsync(Guid userId, CompleteProfileRequest request);
    Task<AuthResponse> RegisterArtistAsync(RegisterArtistRequest request);
    Task<AuthResponse> RegisterContractorAsync(RegisterContractorRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    /// <summary>MVP sem e-mail: em Development retorna o token de reset; em produção, nulo (enviaria por e-mail).</summary>
    Task<string?> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordRequest request);
}
