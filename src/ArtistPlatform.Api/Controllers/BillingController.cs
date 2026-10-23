using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Infrastructure.Asaas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController(IBillingService billingService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// Cria a sessão de checkout da assinatura "ACESSO TOTAL A PLATAFORMA" (R$ 49,90/mês) no Asaas
    /// e retorna id/link/status — o frontend redireciona o cliente direto para o link.
    /// </summary>
    [HttpPost("checkout")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<CheckoutResponseDto>> CreateCheckout()
        => Ok(await billingService.CreateCheckoutAsync(currentUser.EnsureAuthenticated()));

    /// <summary>Última cobrança do artista logado (null se nunca abriu um checkout).</summary>
    [HttpGet("me")]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<BillingDto?>> MyBilling()
        => Ok(await billingService.GetLatestForUserAsync(currentUser.EnsureAuthenticated()));
}

/// <summary>
/// Recepção dos webhooks do Asaas. O payload é aceito em qualquer formato; em
/// event = PAYMENT_CONFIRMED o campo checkoutSession identifica a cobrança e promove
/// a conta de Gratuito para Assinante.
/// </summary>
[ApiController]
[Route("api/webhooks")]
public class WebhooksController(IBillingService billingService, IOptions<AsaasOptions> asaasOptions) : ControllerBase
{
    [HttpPost("asaas")]
    [AllowAnonymous]
    public async Task<ActionResult<WebhookResultDto>> Asaas([FromBody] JsonElement payload)
    {
        // Segredo compartilhado opcional: se configurado, valida o header enviado pelo Asaas.
        var expectedToken = asaasOptions.Value.WebhookToken;
        if (!string.IsNullOrWhiteSpace(expectedToken))
        {
            var received = Request.Headers["asaas-access-token"].FirstOrDefault();
            if (received != expectedToken)
                throw new ForbiddenException("Token de webhook inválido.");
        }

        return Ok(await billingService.HandleWebhookAsync(payload.GetRawText()));
    }
}
