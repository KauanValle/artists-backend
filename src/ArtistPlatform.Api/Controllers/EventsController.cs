using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Agenda do artista (PRD §8): mês/semana/dia via from/to; conflitos por RN009.</summary>
[ApiController]
[Route("api/events")]
[Authorize(Roles = "Artist")]
public class EventsController(IEventService eventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] EventType? type, [FromQuery] EventStatus? status)
        => Ok(await eventService.ListAsync(from, to, type, status));

    [HttpPost]
    public async Task<ActionResult<EventDto>> Create(CreateEventRequestDto request)
        => Ok(await eventService.CreateAsync(request));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDetailDto>> Get(Guid id)
        => Ok(await eventService.GetAsync(id));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EventDto>> Update(Guid id, UpdateEventRequestDto request)
        => Ok(await eventService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await eventService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Checklist de equipamentos por evento (PRD §14).</summary>
    [HttpPost("{id:guid}/equipment")]
    public async Task<ActionResult<EventEquipmentDto>> AddEquipment(Guid id, AddEventEquipmentRequestDto request)
        => Ok(await eventService.AddEquipmentAsync(id, request));

    [HttpPut("{id:guid}/equipment/{eventEquipmentId:guid}/toggle")]
    public async Task<IActionResult> ToggleEquipment(Guid id, Guid eventEquipmentId)
    {
        await eventService.ToggleEquipmentAsync(id, eventEquipmentId);
        return NoContent();
    }

    [HttpDelete("{id:guid}/equipment/{eventEquipmentId:guid}")]
    public async Task<IActionResult> RemoveEquipment(Guid id, Guid eventEquipmentId)
    {
        await eventService.RemoveEquipmentAsync(id, eventEquipmentId);
        return NoContent();
    }

    /// <summary>Sobrescrita da divisão de cachê por evento (RN013).</summary>
    [HttpPut("{id:guid}/team-shares")]
    public async Task<ActionResult<EventTeamShareDto>> SetTeamShare(Guid id, SetEventTeamShareRequestDto request)
        => Ok(await eventService.SetTeamShareAsync(id, request));

    [HttpDelete("{id:guid}/team-shares/{teamMemberId:guid}")]
    public async Task<IActionResult> RemoveTeamShare(Guid id, Guid teamMemberId)
    {
        await eventService.RemoveTeamShareAsync(id, teamMemberId);
        return NoContent();
    }

    /// <summary>Cálculo da divisão: cachê − despesas = distribuível (PRD §13).</summary>
    [HttpGet("{id:guid}/division")]
    public async Task<ActionResult<TeamDivisionDto>> GetDivision(Guid id)
        => Ok(await eventService.GetDivisionAsync(id));
}
