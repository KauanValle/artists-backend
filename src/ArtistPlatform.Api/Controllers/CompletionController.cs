using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Conclusão do show em dupla confirmação (artista + contratante).</summary>
[ApiController]
[Route("api/bookings")]
public class CompletionController(IBookingService bookingService) : ControllerBase
{
    /// <summary>Confirma a conclusão do show (parte logada); devolve o link do contratante quando pendente.</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Artist,Contractor,Admin")]
    public async Task<ActionResult<CompleteShowDto>> Complete(Guid id)
        => Ok(await bookingService.CompleteAsync(id));
}

/// <summary>
/// Endpoints públicos de confirmação de conclusão via token (link gerado pelo evento).
/// Usado quando o contratante confirma fora da plataforma, pelo link enviado pelo artista.
/// </summary>
[ApiController]
[Route("api/public/completion")]
public class PublicCompletionController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<CompletionSummaryDto>> GetByToken(string token)
        => Ok(await bookingService.GetCompletionByTokenAsync(token));

    /// <summary>Confirma a conclusão pelo link (contratante sem login).</summary>
    [HttpPost("{token}/confirm")]
    [AllowAnonymous]
    public async Task<ActionResult<CompletionSummaryDto>> ConfirmByToken(string token)
        => Ok(await bookingService.ConfirmCompletionByTokenAsync(token));
}
