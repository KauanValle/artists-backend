using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ArtistPlatform.Infrastructure.Asaas;

/// <summary>POST /checkouts da API do Asaas — assinatura mensal de acesso à plataforma.</summary>
public class AsaasCheckoutClient(HttpClient http, IOptions<AsaasOptions> optionsSnapshot) : IAsaasCheckoutClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private AsaasOptions Options => optionsSnapshot.Value;

    public async Task<CheckoutResponseDto> CreateCheckoutAsync(Guid userId)
    {
        if (string.IsNullOrWhiteSpace(Options.AccessToken))
            throw new BusinessRuleException("Asaas não configurado: defina Asaas__AccessToken no ambiente.");
        if (string.IsNullOrWhiteSpace(Options.SuccessUrl)
            || string.IsNullOrWhiteSpace(Options.CancelUrl)
            || string.IsNullOrWhiteSpace(Options.ExpiredUrl))
            throw new BusinessRuleException("Asaas não configurado: defina Asaas__SuccessUrl, Asaas__CancelUrl e Asaas__ExpiredUrl no ambiente.");

        // endDate/nextDueDate: 30 dias a partir de agora (ciclo mensal).
        // nextDueDate: hoje, na hora atual ( DateTime.Now = fuso local/Brasília; UtcNow de madrugada
        // cairia no dia seguinte). A cobrança acontece já.
        // endDate: 30 dias a partir de agora (ciclo mensal).
        var now = DateTime.Now;
        var dueDate = now.ToString("yyyy-MM-dd HH:mm:ss");
        var endNewDate = now.AddDays(30).ToString("yyyy-MM-dd HH:mm:ss");

        var payload = new
        {
            billingTypes = new[] { "CREDIT_CARD" },
            chargeTypes = new[] { "RECURRENT" },
            minutesToExpire = Options.MinutesToExpire,
            externalReference = userId.ToString(),
            callback = new
            {
                successUrl = Options.SuccessUrl,
                cancelUrl = Options.CancelUrl,
                expiredUrl = Options.ExpiredUrl
            },
            items = new[]
            {
                new
                {
                    description = Options.PlanDescription,
                    name = Options.PlanName,
                    quantity = 1,
                    value = Options.PlanValue
                }
            },
            subscription = new
            {
                cycle = "MONTHLY",
                endDate = endNewDate,
                nextDueDate = dueDate
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Options.ApiBaseUrl.TrimEnd('/')}/checkouts")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(Options.UserAgent);
        request.Headers.Add("access_token", Options.AccessToken);

        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new BusinessRuleException($"Asaas recusou a criação do checkout ({(int)response.StatusCode}): {raw}");

        using var json = JsonDocument.Parse(raw);
        var id = json.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
        var link = json.RootElement.TryGetProperty("link", out var linkProp) ? linkProp.GetString() : null;
        var status = json.RootElement.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(link))
            throw new BusinessRuleException("Resposta do Asaas sem id/link do checkout.");

        return new CheckoutResponseDto(id, link, status ?? "UNKNOWN");
    }
}
