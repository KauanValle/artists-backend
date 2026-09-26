using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Solicitação de contratação (PRD §18) — estado inicial REQUESTED.</summary>
public class BookingRequest : BaseEntity
{
    public Guid ArtistId { get; set; }
    public Guid ContractorId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? AcceptedProposalId { get; set; }
    public DateOnly EventDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Location { get; set; } = default!;
    public EventType EventType { get; set; } = EventType.Show;
    public int? EstimatedAudience { get; set; }
    public decimal? Budget { get; set; }
    public string Message { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Requested;

    public Interval ToInterval() => new(
        EventDate.ToDateTime(StartTime),
        EventDate.ToDateTime(EndTime));

    /// <summary>Estados de onde a contratação ainda pode evoluir. Rejeitada/expirada/cancelada são terminais (RN018).</summary>
    public bool CanEvolve =>
        Status is BookingStatus.Requested
            or BookingStatus.Negotiating
            or BookingStatus.Proposed;
}
