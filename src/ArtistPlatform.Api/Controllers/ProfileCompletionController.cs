using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Conclusão do perfil pós-registro: escolhe Artista ou Contratante e preenche os dados.</summary>
[ApiController]
[Route("api/perfil")]
public class ProfileCompletionController(IIdentityService identityService) : ControllerBase
{
    /// <summary>
    /// Define o tipo de perfil e cria o registro definitivo (artista ou contratante,
    /// com CPF/CEP). Reemite o token com o papel definitivo.
    /// </summary>
    [HttpPost("completar")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Completar(CompleteProfileRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(await identityService.CompleteProfileAsync(userId, request));
    }
}
