using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Infrastructure.Payments;

/// <summary>
/// Configuração da API de links do InfinityPay — valores do ambiente
/// (InfinityPay__Handle, InfinityPay__RedirectUrl, InfinityPay__WebhookUrl).
/// </summary>
public class InfinityPayOptions
{
    public const string SectionName = "InfinityPay";

    public string ApiBaseUrl { get; set; } = "https://api.checkout.infinitepay.io";
    public string Handle { get; set; } = string.Empty;

    /// <summary>Para onde o comprador volta ao finalizar o checkout (tela informativa).</summary>
    public string RedirectUrl { get; set; } = string.Empty;

    /// <summary>Webhook que recebe a confirmação oficial do pagamento (?ref={paymentId}).</summary>
    public string WebhookUrl { get; set; } = string.Empty;
}
