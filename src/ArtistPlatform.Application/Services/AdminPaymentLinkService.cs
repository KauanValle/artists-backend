using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public record CreateAdminPaymentLinkRequestDto(string Description, decimal Amount, string? CustomerName, string? Note);

public record AdminPaymentLinkDto(
    Guid Id, string Description, decimal Amount, string? CustomerName, string? Note,
    string Status, string CheckoutUrl, DateTime CreatedAt, DateTime? PaidAt);

/// <summary>Visão pública da cobrança (página /pagar/{id}): sem observação interna; checkout só enquanto pendente.</summary>
public record PublicPaymentLinkDto(
    Guid Id, string Description, decimal Amount, string? CustomerName,
    string Status, string? CheckoutUrl, DateTime CreatedAt, DateTime? PaidAt);

public interface IAdminPaymentLinkService
{
    /// <summary>Dados públicos do link; se estiver pendente, confere o pagamento no InfinityPay. Null se não existir.</summary>
    Task<PublicPaymentLinkDto?> GetPublicAsync(Guid id);

    Task<AdminPaymentLinkDto> CreateAsync(Guid adminId, CreateAdminPaymentLinkRequestDto request);
    Task<List<AdminPaymentLinkDto>> ListAsync();
    /// <summary>Consulta o InfinityPay (payment_check) e atualiza o status se já estiver pago.</summary>
    Task<AdminPaymentLinkDto> RefreshAsync(Guid id);
    Task<AdminPaymentLinkDto> CancelAsync(Guid id);
    /// <summary>Webhook do InfinityPay. Retorna null se a referência não for de um link do admin.</summary>
    Task<WebhookResultDto?> HandleWebhookAsync(Guid linkRef, string body);
}

public class AdminPaymentLinkService(IAppDbContext db, IInfinityPayClient infinityPay) : IAdminPaymentLinkService
{
    private const decimal MaxAmount = 100_000m;

    private static readonly string[] ConfirmationMarkers =
        ["paid", "confirmed", "succeeded", "approved", "captured", "completed", "settled", "authorized"];

    public async Task<AdminPaymentLinkDto> CreateAsync(Guid adminId, CreateAdminPaymentLinkRequestDto request)
    {
        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length < 3 || description.Length > 200)
            throw new ValidationException("Informe uma descrição de 3 a 200 caracteres.");

        var amount = Math.Round(request.Amount, 2);
        if (amount < 1m || amount > MaxAmount)
            throw new ValidationException($"O valor deve estar entre R$ 1,00 e R$ {MaxAmount:N0}.");

        var customer = string.IsNullOrWhiteSpace(request.CustomerName) ? null : request.CustomerName.Trim();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (customer is { Length: > 120 }) throw new ValidationException("Nome do cliente muito longo (máx. 120).");
        if (note is { Length: > 500 }) throw new ValidationException("Observação muito longa (máx. 500).");

        var link = new AdminPaymentLink
        {
            Description = description,
            Amount = amount,
            CustomerName = customer,
            Note = note,
            CreatedByUserId = adminId
        };

        // O Id do registro é o order_nsu do checkout: a criação do link no gateway acontece antes de gravar,
        // então um erro do InfinityPay não deixa lixo no banco.
        var result = await infinityPay.CreateLinkAsync(link.Id,
            [new InfinityPayItem((int)Math.Round(amount * 100m), description)]);

        link.CheckoutUrl = result.CheckoutUrl;
        link.TransactionId = string.IsNullOrWhiteSpace(result.TransactionId) ? null : result.TransactionId;
        db.AdminPaymentLinks.Add(link);
        await db.SaveChangesAsync();
        return ToDto(link);
    }

    public async Task<PublicPaymentLinkDto?> GetPublicAsync(Guid id)
    {
        var link = await db.AdminPaymentLinks.FirstOrDefaultAsync(l => l.Id == id);
        if (link is null) return null;

        if (link.Status == AdminPaymentLinkStatus.AwaitingPayment)
        {
            try
            {
                if (await infinityPay.IsPaidAsync(link.Id.ToString(), link.TransactionId))
                    await MarkPaidAsync(link);
            }
            catch (Exception)
            {
                // Falha do gateway não impede de exibir a cobrança; o webhook ainda pode confirmar.
            }
        }

        return new PublicPaymentLinkDto(
            link.Id, link.Description, link.Amount, link.CustomerName, link.Status,
            link.Status == AdminPaymentLinkStatus.AwaitingPayment ? link.CheckoutUrl : null,
            link.CreatedAt, link.PaidAt);
    }

    public async Task<List<AdminPaymentLinkDto>> ListAsync()
    {
        var items = await db.AdminPaymentLinks.AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Take(200)
            .ToListAsync();
        return items.Select(ToDto).ToList();
    }

    public async Task<AdminPaymentLinkDto> RefreshAsync(Guid id)
    {
        var link = await Load(id);
        if (link.Status == AdminPaymentLinkStatus.AwaitingPayment)
        {
            var paid = await infinityPay.IsPaidAsync(link.Id.ToString(), link.TransactionId)
                || (!string.IsNullOrWhiteSpace(link.TransactionId)
                    && await infinityPay.IsPaidAsync(link.Id.ToString()));
            if (paid) await MarkPaidAsync(link);
        }
        return ToDto(link);
    }

    public async Task<AdminPaymentLinkDto> CancelAsync(Guid id)
    {
        var link = await Load(id);
        if (link.Status == AdminPaymentLinkStatus.Paid)
            throw new BusinessRuleException("Link já pago não pode ser cancelado.");
        link.Status = AdminPaymentLinkStatus.Cancelled;
        link.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(link);
    }

    public async Task<WebhookResultDto?> HandleWebhookAsync(Guid linkRef, string body)
    {
        var link = await db.AdminPaymentLinks.FirstOrDefaultAsync(l => l.Id == linkRef);
        if (link is null) return null;

        if (link.Status == AdminPaymentLinkStatus.Paid)
            return new WebhookResultDto(true, "Link já confirmado anteriormente.");

        var content = body.ToLowerInvariant();
        if (!ConfirmationMarkers.Any(m => content.Contains(m)))
            return new WebhookResultDto(false, "Evento não indica pagamento confirmado — ignorado.");

        // Pagamento confirmado vale mesmo que o link tenha sido cancelado (o dinheiro já entrou).
        await MarkPaidAsync(link);
        return new WebhookResultDto(true, "Pagamento do link confirmado.");
    }

    private async Task MarkPaidAsync(AdminPaymentLink link)
    {
        link.Status = AdminPaymentLinkStatus.Paid;
        link.PaidAt = DateTime.UtcNow;
        link.UpdatedAt = link.PaidAt;
        await db.SaveChangesAsync();
    }

    private async Task<AdminPaymentLink> Load(Guid id) =>
        await db.AdminPaymentLinks.FirstOrDefaultAsync(l => l.Id == id)
        ?? throw new NotFoundException("Link de pagamento não encontrado.");

    private static AdminPaymentLinkDto ToDto(AdminPaymentLink l) =>
        new(l.Id, l.Description, l.Amount, l.CustomerName, l.Note, l.Status, l.CheckoutUrl, l.CreatedAt, l.PaidAt);
}
