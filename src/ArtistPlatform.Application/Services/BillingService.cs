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
    /// Processa o webhook do Asaas: em PAYMENT_CONFIRMED localiza a cobrança pelo checkoutSession
    /// e promove a conta de Gratuito para Assinante. Qualquer outro evento é apenas reconhecido.
    /// </summary>
    Task<WebhookResultDto> HandleWebhookAsync(string body);
}

public class BillingService(
    IAppDbContext db,
    IAsaasCheckoutClient asaas,
    ISubscriptionActivator subscriptionActivator,
    INotificationService notifications) : IBillingService
{
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
            if (!string.Equals(evt, "PAYMENT_CONFIRMED", StringComparison.OrdinalIgnoreCase))
                return new WebhookResultDto(false, $"Evento '{evt ?? "(ausente)"}' ignorado — aguardando PAYMENT_CONFIRMED.");

            var checkoutSession = root.TryGetProperty("checkoutSession", out var sessionProp) ? sessionProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(checkoutSession))
                return new WebhookResultDto(false, "Evento sem checkoutSession — não é possível identificar a cobrança.");

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
}
