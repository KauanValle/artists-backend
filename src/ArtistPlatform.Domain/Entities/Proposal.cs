using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Proposta formal (PRD §20): itens + deslocamento + equipamento − desconto = valor final.</summary>
public class Proposal : BaseEntity
{
    public Guid BookingRequestId { get; set; }
    public Guid ArtistId { get; set; }
    public Guid ContractorId { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public List<ProposalItem> Items { get; set; } = [];
    public decimal TravelCost { get; set; }
    public decimal EquipmentCost { get; set; }
    public decimal Discount { get; set; }
    public decimal FinalAmount { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime? RespondedAt { get; set; }

    public decimal CalculateFinalAmount()
    {
        var itemsTotal = Items.Sum(i => i.Amount);
        return itemsTotal + TravelCost + EquipmentCost - Discount;
    }

    /// <summary>RN019: proposta expirada não pode ser aceita.</summary>
    public bool IsExpired => Status == ProposalStatus.Pending && DateTime.UtcNow > ValidUntil;

    public bool CanBeAccepted =>
        Status == ProposalStatus.Pending && DateTime.UtcNow <= ValidUntil;
}
