using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Redes sociais do artista (por enquanto Instagram) exibidas na página pública.</summary>
[ApiController]
[Route("api/social-links")]
public class SocialLinksController(ISocialLinkService socialLinkService) : ControllerBase
{
    [HttpGet("{artistId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<List<SocialLinkDto>>> List(Guid artistId)
        => Ok(await socialLinkService.ListByArtistAsync(artistId));

    /// <summary>Salva o @ do Instagram do artista logado; handle vazio remove o vínculo.</summary>
    [HttpPut]
    [Authorize(Roles = "Artist")]
    public async Task<IActionResult> Save(SaveSocialLinkRequest request)
    {
        var result = await socialLinkService.SaveAsync(Guid.Empty, request);
        return result is null ? NoContent() : Ok(result);
    }
}
