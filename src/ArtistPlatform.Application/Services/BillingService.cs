using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IBillingService
{
    /// <summary>Cria a sessão de checkout da assinatura no Asaas e persiste id/link/status.</summary>
    Task<CheckoutResponseDto> CreateCheckoutAsync(Guid userId);

    /// <summary>Última cobrança do usuário (ou null se nunca assinou).</summary>
    Task<BillingDto?> GetLatestForUserAsync(Guid userId);

    /// <summary>
    /// Processa o webhook do Asaas: em SUBSCRIPTION_CREATED ou PAYMENT_CONFIRMED localiza a cobrança
    /// pelo checkoutSession (na raiz ou aninhado em payment/subscription) e promove a conta de
    /// Gratuito para Assinante. Qualquer outro evento é apenas reconhecido.
    /// </summary>
    Task<WebhookResultDto> HandleWebhookAsync(string body);
}

public class BillingService(
    IAppDbContext db,
    IAsaasCheckoutClient asaas,
    ISubscriptionActivator subscriptionActivator,
    INotificationService notifications) : IBillingService
{
    /// <summary>Eventos que indicam assinatura paga/ativa no fluxo de cartão do Asaas.</summary>
    private static readonly string[] ActivationEvents = ["SUBSCRIPTION_CREATED", "PAYMENT_CONFIRMED"];

    public async Task<CheckoutResponseDto> CreateCheckoutAsync(Guid userId)
    {
        // Nova sessão a cada clique: a anterior expira em poucos minutos (minutesToExpire do Asaas).
        var checkout = await asaas.CreateCheckoutAsync(userId);

        var billing = new Billing
        {
            UserId = userId,
            CheckoutSessionId = checkout.CheckoutSessionId,
            Link = checkout.Link,
            Status = checkout.Status
        };
        db.Billings.Add(billing);
        await db.SaveChangesAsync();

        return checkout;
    }

    public async Task<BillingDto?> GetLatestForUserAsync(Guid userId)
    {
        return await db.Billings.AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BillingDto(b.Id, b.CheckoutSessionId, b.Link, b.Status, b.CreatedAt))
            .FirstOrDefaultAsync();
    }

    public async Task<WebhookResultDto> HandleWebhookAsync(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new WebhookResultDto(false, "Payload não é um JSON válido.");
        }

        using (document)
        {
            var root = document.RootElement;

            var evt = root.TryGetProperty("event", out var eventProp) ? eventProp.GetString() : null;
            if (evt is null || !ActivationEvents.Contains(evt, StringComparer.OrdinalIgnoreCase))
                return new WebhookResultDto(false, $"Evento '{evt ?? "(ausente)"}' ignorado — aguardando SUBSCRIPTION_CREATED ou PAYMENT_CONFIRMED.");

            var checkoutSession = ExtractCheckoutSession(root);
            if (string.IsNullOrWhiteSpace(checkoutSession))
                return new WebhookResultDto(false, "Evento sem checkoutSession (raiz, payment ou subscription) — não é possível identificar a cobrança.");

            var billing = await db.Billings.FirstOrDefaultAsync(b => b.CheckoutSessionId == checkoutSession);
            if (billing is null)
                return new WebhookResultDto(false, $"Nenhuma cobrança encontrada para a sessão {checkoutSession}.");

            var changed = await subscriptionActivator.ActivateAsync(billing.UserId);
            billing.Status = "CONFIRMED";
            billing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            if (changed)
                await notifications.NotifyAsync(
                    billing.UserId,
                    NotificationType.SubscriptionConfirmed,
                    "Assinatura confirmada!",
                    "Pagamento confirmado — acesso total à plataforma liberado.",
                    "/artist/dashboard");

            return new WebhookResultDto(true, changed ? "Assinatura ativada." : "Assinatura já estava ativa.");
        }
    }

    /// <summary>checkoutSession pode vir na raiz ou aninhado em payment/subscription (payload real do Asaas).</summary>
    private static string? ExtractCheckoutSession(JsonElement root)
    {
        if (TryGetStringProperty(root, "checkoutSession", out var direct))
            return direct;

        if (root.TryGetProperty("payment", out var payment) && payment.ValueKind == JsonValueKind.Object
            && TryGetStringProperty(payment, "checkoutSession", out var fromPayment))
            return fromPayment;

        if (root.TryGetProperty("subscription", out var subscription) && subscription.ValueKind == JsonValueKind.Object
            && TryGetStringProperty(subscription, "checkoutSession", out var fromSubscription))
            return fromSubscription;

        return null;
    }

    private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
            return false;
        if (property.ValueKind != JsonValueKind.String)
            return false;
        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }
}
