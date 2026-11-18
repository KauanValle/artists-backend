using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Tipo de ingresso de um <see cref="TicketEvent"/> (ex.: "Pista", "VIP", "Meia-entrada").
/// O produtor informa o valor que quer RECEBER; o valor pago pelo comprador é esse valor
/// acrescido da taxa administrativa vigente no momento do cadastro (guardada em FeePercent).
/// </summary>
public class EventTicket : BaseEntity
{
    public Guid TicketEventId { get; set; }

    public string Title { get; set; } = default!;

    /// <summary>Quantidade de ingressos disponíveis para venda.</summary>
    public int Quantity { get; set; }

    /// <summary>Valor líquido que o produtor recebe por ingresso.</summary>
    public decimal ReceiveAmount { get; set; }

    /// <summary>Percentual da taxa administrativa aplicado (snapshot no cadastro).</summary>
    public decimal FeePercent { get; set; }

    /// <summary>Valor pago pelo comprador: ReceiveAmount + taxa.</summary>
    public decimal BuyerPrice { get; set; }

    /// <summary>Período de vendas (datas, sem horário).</summary>
    public DateOnly SalesStartDate { get; set; }
    public DateOnly SalesEndDate { get; set; }

    public string Description { get; set; } = string.Empty;
}
