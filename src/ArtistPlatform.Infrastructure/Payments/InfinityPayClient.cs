using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Infrastructure.Payments;

/// <summary>
/// POST /links da API do InfinityPay — gera o checkout do cachê do show.
/// O webhook_url leva ?ref={paymentId} para correlacionar a confirmação
/// com o pagamento persistido, independente do formato do payload do gateway.
/// </summary>
public class InfinityPayClient(HttpClient http, IOptions<InfinityPayOptions> optionsSnapshot) : IInfinityPayClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private InfinityPayOptions Options => optionsSnapshot.Value;

    public async Task<InfinityPayLinkResult> CreateLinkAsync(InfinityPayLinkRequest request)
    {
        if (string.IsNullOrWhiteSpace(Options.Handle))
            throw new BusinessRuleException("InfinityPay não configurado: defina InfinityPay__Handle no ambiente.");
        if (string.IsNullOrWhiteSpace(Options.RedirectUrl) || string.IsNullOrWhiteSpace(Options.WebhookUrl))
            throw new BusinessRuleException("InfinityPay não configurado: defina InfinityPay__RedirectUrl e InfinityPay__WebhookUrl no ambiente.");

        // Preço em centavos (R$ 400,00 = 40000).
        var priceCents = (int)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);

        var payload = new
        {
            handle = Options.Handle,
            redirect_url = Options.RedirectUrl,
            webhook_url = $"{Options.WebhookUrl}?ref={request.PaymentRef}",
            items = new[]
            {
                new
                {
                    quantity = 1,
                    price = priceCents,
                    description = request.Description
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{Options.ApiBaseUrl.TrimEnd('/')}/links")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(httpRequest);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new BusinessRuleException($"InfinityPay recusou a criação do link ({(int)response.StatusCode}): {raw}");

        using var json = JsonDocument.Parse(raw);
        var root = json.RootElement;

        var url = GetString(root, "url") ?? GetString(root, "checkout_url") ?? GetString(root, "link");
        if (string.IsNullOrWhiteSpace(url))
            throw new BusinessRuleException($"InfinityPay devolveu um link inesperado: {raw}");

        var transactionId = GetString(root, "id") ?? GetString(root, "uuid") ?? string.Empty;

        return new InfinityPayLinkResult(transactionId, url);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return null;
        if (property.ValueKind != JsonValueKind.String)
            return null;
        return property.GetString();
    }
}
