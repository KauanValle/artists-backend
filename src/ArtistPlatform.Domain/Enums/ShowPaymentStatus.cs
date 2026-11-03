namespace ArtistPlatform.Domain.Enums;

/// <summary>Status do pagamento do show — separado do status da contratação para
/// suportar futuros fluxos (recusa, reembolso, cancelamento).</summary>
public enum ShowPaymentStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}
