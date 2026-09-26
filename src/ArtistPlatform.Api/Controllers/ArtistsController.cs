using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/artists")]
public class ArtistsController(IArtistService artistService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Busca do marketplace (PRD §17) — pública; RN020 valida o intervalo exato.</summary>
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<ActionResult<PagedResult<ArtistCardDto>>> Search([FromQuery] SearchArtistsQuery query)
        => Ok(await artistService.SearchAsync(query));

    /// <summary>Perfil público por id.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtistDetailPublicDto>> GetPublic(Guid id)
        => Ok(await artistService.GetPublicAsync(id));

    /// <summary>Perfil público por slug — URL /artistas/{slug} (PRD §15).</summary>
    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtistDetailPublicDto>> GetBySlug(string slug)
        => Ok(await artistService.GetBySlugAsync(slug));

    /// <summary>Perfil do artista autenticado (operacional).</summary>
    [HttpGet("me")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<ArtistDto>> GetMy()
        => Ok(await artistService.GetMyArtistAsync(currentUser.UserId!.Value));

    /// <summary>Onboarding/atualização do perfil (PRD §6).</summary>
    [HttpPut("me")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<ArtistDto>> SaveOnboarding(SaveArtistRequest request)
        => Ok(await artistService.SaveOnboardingAsync(currentUser.UserId!.Value, request));

    /// <summary>Publica ou despublica o perfil público.</summary>
    [HttpPut("me/publish")]
    [Authorize(Roles = "Artist")]
    public async Task<IActionResult> Publish([FromBody] PublishRequest request)
    {
        await artistService.PublishAsync(currentUser.UserId!.Value, request.Publish);
        return NoContent();
    }

    public record PublishRequest(bool Publish);
}

[ApiController]
[Route("api/categories")]
public class CategoriesController(IArtistService artistService) : ControllerBase
{
    /// <summary>Categorias para cadastro/busca (cantor, banda, DJ, ...).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<CategoryDto>>> List()
        => Ok(await artistService.GetCategoriesAsync());
}
