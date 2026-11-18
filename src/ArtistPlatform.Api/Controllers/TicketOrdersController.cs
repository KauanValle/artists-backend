using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Compra pública de ingressos (sem login).</summary>
[ApiController]
[Route("api/public/ticket-events/{eventId:guid}/orders")]
[AllowAnonymous]
public class PublicTicketOrdersController(ITicketOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketOrderDto>> Create(Guid eventId, CreateTicketOrderRequestDto request)
        => Ok(await orderService.CreateAsync(eventId, request));
}
