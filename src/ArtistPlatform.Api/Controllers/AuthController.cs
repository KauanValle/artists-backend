using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IIdentityService identityService) : ControllerBase
{
    /// <summary>Cadastro de artista (cria usuário + perfil básico).</summary>
    [HttpPost("register/artist")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterArtist(RegisterArtistRequest request)
        => Ok(await identityService.RegisterArtistAsync(request));

    /// <summary>Cadastro de contratante.</summary>
    [HttpPost("register/contractor")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RegisterContractor(RegisterContractorRequest request)
        => Ok(await identityService.RegisterContractorAsync(request));

    /// <summary>Login por e-mail e senha; retorna access + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        => Ok(await identityService.LoginAsync(request));

    /// <summary>Renova a sessão a partir do refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshTokenRequest request)
        => Ok(await identityService.RefreshTokenAsync(request.RefreshToken));

    /// <summary>Revoga o refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        await identityService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    /// <summary>Solicita recuperação de senha. Em Development retorna o token no corpo (MVP sem e-mail).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        var token = await identityService.ForgotPasswordAsync(request.Email);
        return Ok(new { token });
    }

    /// <summary>Redefine a senha com o token de recuperação.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await identityService.ResetPasswordAsync(request);
        return NoContent();
    }

    /// <summary>Dados do usuário autenticado.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        return Ok(await identityService.GetCurrentUserAsync(userId));
    }
}
