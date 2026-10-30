using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Conteúdo do EPK público do artista (/epk/{slug}) editável em "Meu EPK".</summary>
[ApiController]
[Route("api/epk")]
public class EpkController(IEpkService epkService) : ControllerBase
{
    [HttpGet("{artistId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EpkDto>> Get(Guid artistId)
        => Ok(await epkService.GetByArtistAsync(artistId));

    /// <summary>Salva o EPK do artista logado (por enquanto a bio específica).</summary>
    [HttpPut]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<EpkDto>> Save(SaveEpkRequest request)
        => Ok(await epkService.SaveAsync(Guid.Empty, request));
}
