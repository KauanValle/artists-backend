using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Sessão de checkout da assinatura (Asaas): guarda id, link de pagamento e status do retorno.
/// O webhook de confirmação localiza a cobrança pelo CheckoutSessionId para ativar a conta.
/// </summary>
public class Billing : BaseEntity
{
    public Guid UserId { get; set; }
    public string CheckoutSessionId { get; set; } = default!;
    public string Link { get; set; } = default!;
    public string Status { get; set; } = default!;
}
