using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

public enum WithdrawalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Paid = 4
}

/// <summary>
/// Solicitação de saque do artista — criada pelo artista, aprovada/rejeitada pelo admin.
/// </summary>
public class WithdrawalRequest : BaseEntity
{
    public Guid ArtistId { get; set; }

    /// <summary>Valor solicitado para saque (já com desconto da taxa).</summary>
    public decimal Amount { get; set; }

    /// <summary>Percentual da taxa administrativa no momento da solicitação (snapshot).</summary>
    public decimal TaxaPercentual { get; set; }

    /// <summary>Valor total da taxa descontada.</summary>
    public decimal TaxaValor { get; set; }

    /// <summary>Valor bruto dos shows (antes da taxa).</summary>
    public decimal ValorBruto { get; set; }

    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

    /// <summary>Observação do admin ao aprovar/rejeitar.</summary>
    public string? AdminNotes { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
