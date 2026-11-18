using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Pedido de compra de ingressos feito na página pública do evento (sem login).
/// Nesta etapa o pedido nasce "AwaitingPayment" e já reserva o estoque; o pagamento entra depois.
/// </summary>
public class TicketOrder : BaseEntity
{
    public Guid TicketEventId { get; set; }
    public Guid EventTicketId { get; set; }

    public string BuyerName { get; set; } = default!;
    /// <summary>Somente dígitos (DDD + número).</summary>
    public string BuyerPhone { get; set; } = default!;
    public string BuyerEmail { get; set; } = default!;
    /// <summary>Somente dígitos (11).</summary>
    public string BuyerCpf { get; set; } = default!;

    public int Quantity { get; set; }

    /// <summary>Preço unitário pago pelo comprador no momento da compra (snapshot).</summary>
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = TicketOrderStatus.AwaitingPayment;

    /// <summary>Id da transação / link no InfinityPay.</summary>
    public string? TransactionId { get; set; }

    /// <summary>Link de checkout do InfinityPay (reaproveitado se o comprador voltar ao pagamento).</summary>
    public string? CheckoutUrl { get; set; }

    public DateTime? PaidAt { get; set; }
}

public static class TicketOrderStatus
{
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
}
