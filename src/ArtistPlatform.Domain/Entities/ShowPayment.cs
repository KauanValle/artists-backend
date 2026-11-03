using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Pagamento do cachê de um show via InfinityPay: link de checkout gerado no aceite
/// da contratação e confirmado pelo webhook. Fica separado do BookingStatus para
/// permitir recusas, reembolsos e retentativas sem mexer no ciclo da contratação.
/// </summary>
public class ShowPayment : BaseEntity
{
    public Guid BookingRequestId { get; set; }
    public Guid ContractorId { get; set; }

    /// <summary>Valor cobrado, em reais — sempre do banco (proposta aceita ou orçamento).</summary>
    public decimal Amount { get; set; }

    /// <summary>Identificador do link/transação no InfinityPay (quando retornado).</summary>
    public string TransactionId { get; set; } = string.Empty;

    public string CheckoutUrl { get; set; } = string.Empty;
    public ShowPaymentStatus Status { get; set; } = ShowPaymentStatus.Pending;
    public DateTime? ConfirmedAt { get; set; }
}
