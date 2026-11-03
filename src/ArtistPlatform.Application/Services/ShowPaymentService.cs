using System.Text.Json;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IShowPaymentService
{
    /// <summary>Gera o link de checkout no InfinityPay para uma contratação aguardando pagamento.</summary>
    Task<CheckoutUrlDto> StartCheckoutAsync(Guid bookingId);

    /// <summary>Processa o webhook do InfinityPay: confirma o pagamento e o show (idempotente).</summary>
    Task<WebhookResultDto> HandleWebhookAsync(Guid? paymentRef, string body);
}

/// <summary>
/// Pagamento do cachê do show via InfinityPay. O aceite da contratação fica em
/// AWAITING_PAYMENT; somente a confirmação oficial do webhook promove o show para
/// CONFIRMED — criando o evento na agenda (com o buffer de deslocamento) e a receita
/// PENDING do artista (RN007/RN008/RN010). O valor cobrado vem sempre do banco.
/// </summary>
public class ShowPaymentService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IInfinityPayClient infinityPay,
    INotificationService notifications) : IShowPaymentService
{
    private static readonly string[] ConfirmationMarkers =
    [
        "paid", "confirmed", "succeeded", "approved", "captured", "completed", "settled", "authorized"
    ];

    public async Task<CheckoutUrlDto> StartCheckoutAsync(Guid bookingId)
    {
        var booking = await db.BookingRequests.FirstOrDefaultAsync(b => b.Id == bookingId)
            ?? throw new NotFoundException("Contratação não encontrada.");

        // Segurança: só o contratante da própria contratação pode pagar.
        if (currentUser.Role != UserRole.Admin && booking.ContractorId != currentUser.ContractorId)
            throw new ForbiddenException("Apenas o contratante desta contratação pode realizar o pagamento.");

        if (booking.Status == BookingStatus.Completed)
            throw new BusinessRuleException("Esta contratação já foi paga e concluída.");
        if (booking.Status != BookingStatus.AwaitingPayment)
            throw new BusinessRuleException("Esta contratação não está aguardando pagamento.");

        // Impede pagamentos duplicados: confirmado no gateway, não gera novo link.
        var jaPago = await db.ShowPayments.AsNoTracking()
            .AnyAsync(p => p.BookingRequestId == bookingId && p.Status == ShowPaymentStatus.Paid);
        if (jaPago)
            throw new BusinessRuleException("Esta contratação já possui um pagamento confirmado.");

        // Valor real vem do banco: proposta aceita, senão orçamento acordado.
        decimal valor;
        if (booking.AcceptedProposalId is not null)
        {
            valor = await db.Proposals.AsNoTracking()
                .Where(p => p.Id == booking.AcceptedProposalId)
                .Select(p => p.FinalAmount)
                .FirstOrDefaultAsync();
            if (valor <= 0)
                throw new BusinessRuleException("A proposta aceita desta contratação não foi encontrada.");
        }
        else
        {
            if (booking.Budget is not > 0)
                throw new BusinessRuleException("Não há valor acordado para esta contratação.");
            valor = booking.Budget.Value;
        }

        var artistName = await db.Artists.AsNoTracking()
            .Where(a => a.Id == booking.ArtistId)
            .Select(a => a.ArtisticName)
            .FirstAsync();

        // Persiste antes de chamar o gateway: o webhook_url leva ?ref={paymentId}.
        var payment = new ShowPayment
        {
            BookingRequestId = booking.Id,
            ContractorId = booking.ContractorId!.Value,
            Amount = valor,
            Status = ShowPaymentStatus.Pending
        };
        db.ShowPayments.Add(payment);
        await db.SaveChangesAsync();

        var link = await infinityPay.CreateLinkAsync(
            new InfinityPayLinkRequest(valor, $"Show - {artistName}", payment.Id));

        payment.TransactionId = link.TransactionId;
        payment.CheckoutUrl = link.CheckoutUrl;
        await db.SaveChangesAsync();

        return new CheckoutUrlDto(link.CheckoutUrl);
    }

    public async Task<WebhookResultDto> HandleWebhookAsync(Guid? paymentRef, string body)
    {
        // Localiza o pagamento: preferência pelo ?ref={id} do webhook_url; senão pelo
        // TransactionId presente no payload. Idempotente: pagamento já pago é apenas reconhecido.
        ShowPayment? payment = null;
        if (paymentRef is not null)
            payment = await db.ShowPayments.FirstOrDefaultAsync(p => p.Id == paymentRef);

        if (payment is null)
        {
            var transactionId = ExtractTransactionId(body);
            if (!string.IsNullOrWhiteSpace(transactionId))
                payment = await db.ShowPayments.FirstOrDefaultAsync(p => p.TransactionId == transactionId);
        }

        if (payment is null)
            return new WebhookResultDto(false, "Nenhum pagamento encontrado para este webhook.");
        if (payment.Status == ShowPaymentStatus.Paid)
            return new WebhookResultDto(true, "Pagamento já confirmado anteriormente.");
        if (payment.Status == ShowPaymentStatus.Cancelled)
            return new WebhookResultDto(false, "Este pagamento foi cancelado.");

        var conteudo = body.ToLowerInvariant();
        var confirmado = ConfirmationMarkers.Any(m => conteudo.Contains(m));
        if (!confirmado)
            return new WebhookResultDto(false, "Evento não indica pagamento confirmado — ignorado.");

        var booking = await db.BookingRequests.FirstAsync(b => b.Id == payment.BookingRequestId);
        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == booking.ArtistId);

        // 1) pagamento confirmado (sempre registrado, mesmo se a contratação foi cancelada depois)
        payment.Status = ShowPaymentStatus.Paid;
        payment.ConfirmedAt = DateTime.UtcNow;

        Guid? eventId = booking.EventId;
        if (booking.Status == BookingStatus.AwaitingPayment)
        {
            // 2) show confirmado
            booking.Status = BookingStatus.Confirmed;
            booking.UpdatedAt = DateTime.UtcNow;

            // 3) evento na agenda: cria (fluxo normal) ou promove o pré-reservado (visitante).
            var interval = booking.ToInterval();
            var buffer = TimeSpan.FromMinutes(artist.TravelTimeMinutes);
            if (booking.EventId is null)
            {
                var ev = new Event
                {
                    ArtistId = booking.ArtistId,
                    ContractorId = booking.ContractorId,
                    BookingRequestId = booking.Id,
                    Title = $"{artist.ArtisticName} — {booking.EventType}",
                    Type = booking.EventType,
                    StartDateTime = interval.Start - buffer,
                    EndDateTime = interval.End + buffer,
                    Location = booking.Location,
                    Description = booking.Message,
                    Status = EventStatus.Confirmed
                };
                db.Events.Add(ev);
                booking.EventId = ev.Id;
                eventId = ev.Id;
            }
            else
            {
                var ev = await db.Events.FirstAsync(e => e.Id == booking.EventId);
                if (ev.Status == EventStatus.PreReserved)
                {
                    ev.Status = EventStatus.Confirmed;
                    ev.UpdatedAt = DateTime.UtcNow;
                }
            }

            // 4) receita PENDING do artista (RN010)
            db.FinancialTransactions.Add(new FinancialTransaction
            {
                ArtistId = booking.ArtistId,
                Type = TransactionType.Income,
                Category = "Show",
                Amount = payment.Amount,
                DueDate = booking.EventDate,
                Status = TransactionStatus.Pending,
                EventId = eventId,
                Notes = $"Receita do pagamento confirmado da contratação {booking.Id} (InfinityPay)."
            });

            await db.SaveChangesAsync();

            // 5) notifica as duas partes
            await notifications.NotifyAsync(artist.UserId, NotificationType.ShowConfirmed,
                "Show confirmado",
                $"Pagamento confirmado! Show garantido para {booking.EventDate:dd/MM/yyyy} às {booking.StartTime} em {booking.Location}.",
                $"/shows/{booking.Id}");
            if (booking.ContractorId is not null)
            {
                var contractorUserId = await db.Contractors.AsNoTracking()
                    .Where(ct => ct.Id == booking.ContractorId)
                    .Select(ct => ct.UserId)
                    .FirstOrDefaultAsync();
                if (contractorUserId != Guid.Empty)
                    await notifications.NotifyAsync(contractorUserId, NotificationType.ShowConfirmed,
                        "Show confirmado",
                        $"Pagamento de {payment.Amount:C} confirmado. O show de {booking.EventDate:dd/MM/yyyy} está garantido com {artist.ArtisticName}.",
                        $"/solicitacoes/{booking.Id}");
            }

            return new WebhookResultDto(true, "Pagamento confirmado e show confirmado.");
        }

        await db.SaveChangesAsync();
        return new WebhookResultDto(true,
            $"Pagamento registrado, mas a contratação está em '{booking.Status}' — show não foi confirmado.");
    }

    private static string? ExtractTransactionId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            foreach (var prop in new[] { "transaction_id", "id", "payment_id", "uuid" })
            {
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty(prop, out var value)
                    && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("data", out var data)
                    && data.ValueKind == JsonValueKind.Object
                    && data.TryGetProperty(prop, out var nested)
                    && nested.ValueKind == JsonValueKind.String)
                    return nested.GetString();
            }
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
