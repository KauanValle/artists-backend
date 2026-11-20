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

    /// <summary>Taxa administrativa (%) aplicada sobre o valor a receber dos ingressos.</summary>
    [HttpGet("fee")]
    public async Task<ActionResult<TicketFeeDto>> GetFee()
        => Ok(await ticketEventService.GetFeeAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketEventDto>> Get(Guid id)
        => Ok(await ticketEventService.GetAsync(id));

    [HttpPost]
    public async Task<ActionResult<TicketEventDto>> Create(SaveTicketEventRequestDto request)
        => Ok(await ticketEventService.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketEventDto>> Update(Guid id, SaveTicketEventRequestDto request)
        => Ok(await ticketEventService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await ticketEventService.DeleteAsync(id);
        return NoContent();
    }
}

/// <summary>Visualização pública do evento (link compartilhável, sem login).</summary>
[ApiController]
[Route("api/public/ticket-events")]
[AllowAnonymous]
public class PublicTicketEventsController(ITicketEventService ticketEventService) : ControllerBase
{
    /// <summary>Lista todos os eventos da plataforma (públicos, mais recentes primeiro).</summary>
    [HttpGet]
    public async Task<ActionResult<List<TicketEventDto>>> ListAll()
        => Ok(await ticketEventService.ListAllPublicAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketEventDto>> Get(Guid id)
        => Ok(await ticketEventService.GetPublicAsync(id));
}
