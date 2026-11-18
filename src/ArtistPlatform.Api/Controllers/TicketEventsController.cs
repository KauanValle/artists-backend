using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>
/// Emissor de ingressos — eventos. Painel único: qualquer usuário autenticado (artista ou
/// contratante) gerencia os próprios eventos.
/// </summary>
[ApiController]
[Route("api/ticket-events")]
[Authorize]
public class TicketEventsController(ITicketEventService ticketEventService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TicketEventDto>>> List()
        => Ok(await ticketEventService.ListAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketEventDto>> Get(Guid id)
        => Ok(await ticketEventService.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<TicketEventDto>> Create(SaveTicketEventRequestDto request)
        => Ok(await ticketEventService.CreateAsync(request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await ticketEventService.DeleteAsync(id);
        return NoContent();
    }
}
