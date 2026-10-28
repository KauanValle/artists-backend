using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/bookings/{bookingId:guid}/contract")]
[Authorize]
public class SignatureContractsController(IContractService contractService) : ControllerBase
{
    /// <summary>Rascunho auto-preenchido ou contrato assinado da contratação.</summary>
    [HttpGet]
    public async Task<ActionResult<ArtistContractDto>> Get(Guid bookingId)
        => Ok(await contractService.GetForBookingAsync(bookingId));

    /// <summary>
    /// Assina o contrato com assinatura desenhada + evidências de formalidade:
    /// hash SHA-256 do texto, data/hora UTC com segundos, IP e user-agent capturados pelo servidor.
    /// </summary>
    [HttpPost("sign")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<ArtistContractDto>> Sign(Guid bookingId, SignArtistContractRequest request)
    {
        var ip = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "desconhecido";
        var userAgent = Request.Headers.UserAgent.ToString();

        return Ok(await contractService.SignAsync(bookingId, request, ip, userAgent));
    }
}
