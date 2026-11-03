using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Pagamento do cachê do show via InfinityPay (fluxo de contratação).</summary>
[ApiController]
[Route("api/payments")]
[Authorize]
public class ShowPaymentsController(IShowPaymentService showPaymentService) : ControllerBase
{
    /// <summary>
    /// Gera o link de checkout no InfinityPay para a contratação aguardando pagamento.
    /// O valor é lido do banco (proposta aceita ou orçamento) — nunca do frontend.
    /// </summary>
    [HttpPost("booking/{bookingId:guid}/checkout")]
    [Authorize(Roles = "Contractor")]
    public async Task<ActionResult<CheckoutUrlDto>> StartCheckout(Guid bookingId)
        => Ok(await showPaymentService.StartCheckoutAsync(bookingId));
}
