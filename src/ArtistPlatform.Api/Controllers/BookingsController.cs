using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    /// <summary>Contratante envia solicitação (estado inicial REQUESTED — PRD §18).</summary>
    [HttpPost]
    [Authorize(Roles = "Contractor")]
    public async Task<ActionResult<BookingDto>> Create(CreateBookingRequestDto request)
        => Ok(await bookingService.CreateAsync(request));

    /// <summary>Lista com filtro por status (abas do PRD §10), escopada ao papel do usuário.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BookingDto>>> List(
        [FromQuery] BookingStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await bookingService.ListAsync(status, page, pageSize));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingDto>> Get(Guid id)
        => Ok(await bookingService.GetAsync(id));

    /// <summary>Artista inicia negociação (REQUESTED → NEGOTIATING).</summary>
    [HttpPost("{id:guid}/negotiate")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<BookingDto>> StartNegotiation(Guid id)
        => Ok(await bookingService.StartNegotiationAsync(id));

    /// <summary>
    /// Aceite direto pelo artista, sem proposta formal: usa o orçamento da solicitação como valor
    /// final, confirma o evento, bloqueia a agenda e cria a receita PENDING (RN007/RN008/RN010/RN017).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<BookingDto>> Accept(Guid id)
        => Ok(await bookingService.AcceptAsync(id));

    /// <summary>Artista recusa (RN018: recusada não volta a ser aceita).</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<BookingDto>> Reject(Guid id)
        => Ok(await bookingService.RejectAsync(id));

    /// <summary>Cancelamento antes da confirmação.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(Guid id)
        => Ok(await bookingService.CancelAsync(id));
}
