using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/artists/{artistId:guid}/availability")]
public class AvailabilityController(IAvailabilityService availabilityService) : ControllerBase
{
    /// <summary>Disponibilidade do artista (pública; usada no perfil e no marketplace).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<AvailabilityDto>>> ListByArtist(
        Guid artistId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await availabilityService.ListByArtistAsync(artistId, from, to));

    /// <summary>Janelas ocupadas por eventos (agendados/confirmados) — pública, para bloquear horários na contratação.</summary>
    [HttpGet("busy-slots")]
    [AllowAnonymous]
    public async Task<ActionResult<List<BusySlotDto>>> BusySlots(
        Guid artistId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await availabilityService.GetBusySlotsAsync(artistId, from, to));
}

[ApiController]
[Route("api/artists/me/availability")]
[Authorize(Roles = "Artist")]
public class MyAvailabilityController(IAvailabilityService availabilityService) : ControllerBase
{
    /// <summary>Cria intervalo(s) de disponibilidade — múltiplos intervalos por dia (PRD §8).</summary>
    [HttpPost]
    public async Task<ActionResult<List<AvailabilityDto>>> Create(SaveAvailabilityRequest request)
        => Ok(await availabilityService.CreateAsync(Guid.Empty, request));

    /// <summary>Criação em lote: vários dias de uma vez (dias de trabalho padrão / agenda visual).</summary>
    [HttpPost("batch")]
    public async Task<ActionResult<BatchAvailabilityResultDto>> CreateBatch(SaveAvailabilityBatchRequest request)
        => Ok(await availabilityService.CreateBatchAsync(Guid.Empty, request));

    /// <summary>Atualiza um intervalo.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AvailabilityDto>> Update(Guid id, SaveAvailabilityRequest request)
        => Ok(await availabilityService.UpdateAsync(Guid.Empty, id, request));

    /// <summary>Remove um intervalo.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await availabilityService.DeleteAsync(Guid.Empty, id);
        return NoContent();
    }
}
