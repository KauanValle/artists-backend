using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Receita/despesa com status de pagamento (PRD §12, RN010–RN012).</summary>
public class FinancialTransaction : BaseEntity
{
    public Guid ArtistId { get; set; }
    public TransactionType Type { get; set; }
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public DateOnly? DueDate { get; set; }
    /// <summary>Data recebida (receita) ou paga (despesa).</summary>
    public DateOnly? SettlementDate { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public Guid? EventId { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>Contas vencidas em aberto ficam OVERDUE.</summary>
    public void RefreshOverdueStatus(DateOnly today)
    {
        if (Status == TransactionStatus.Pending && DueDate.HasValue && DueDate < today)
            Status = TransactionStatus.Overdue;
    }
}
