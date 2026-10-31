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
/// Confirmação de conclusão via token (link gerado pelo evento). O link leva o
/// contratante direto ao show, mas exige login: apenas o contratante da
/// contratação pode visualizar e confirmar.
/// </summary>
[ApiController]
[Route("api/public/completion")]
public class PublicCompletionController(IBookingService bookingService) : ControllerBase
{
    [HttpGet("{token}")]
    [Authorize(Roles = "Contractor,Admin")]
    public async Task<ActionResult<CompletionSummaryDto>> GetByToken(string token)
        => Ok(await bookingService.GetCompletionByTokenAsync(token));

    /// <summary>Confirma a conclusão pelo link (contratante logado).</summary>
    [HttpPost("{token}/confirm")]
    [Authorize(Roles = "Contractor,Admin")]
    public async Task<ActionResult<CompletionSummaryDto>> ConfirmByToken(string token)
        => Ok(await bookingService.ConfirmCompletionByTokenAsync(token));
}
