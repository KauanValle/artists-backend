using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Link de pagamento avulso (InfinityPay) gerado pelo admin: cobrança livre com descrição e valor,
/// sem vínculo com show ou evento. O Id vai como order_nsu e ?ref= do webhook.
/// </summary>
public class AdminPaymentLink : BaseEntity
{
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }

    /// <summary>Quem será cobrado (livre, só para o admin se organizar).</summary>
    public string? CustomerName { get; set; }
    public string? Note { get; set; }

    public string Status { get; set; } = AdminPaymentLinkStatus.AwaitingPayment;
    public string? TransactionId { get; set; }
    public string CheckoutUrl { get; set; } = default!;
    public DateTime? PaidAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}

public static class AdminPaymentLinkStatus
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}
