using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/public/bookings")]
public class PublicBookingsController(IBookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Rota pública (sem cadastro): visitante solicita contratação informando nome do responsável,
    /// estabelecimento, WhatsApp e valor da proposta. O artista recebe a solicitação com o contato.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<BookingDto>> CreateGuest(CreateGuestBookingRequestDto request)
        => Ok(await bookingService.CreateGuestAsync(request));
}
