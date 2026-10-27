using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Solicitação de contratação (PRD §18) — estado inicial REQUESTED.</summary>
/// <remarks>
/// Solicitações de visitante (rota pública, sem cadastro) têm ContractorId nulo e os dados
/// de contato informados na hora (GuestName/GuestCompanyName/GuestPhone); o contato acontece
/// pelo WhatsApp informado — sem conversa interna.
/// </remarks>
public class BookingRequest : BaseEntity
{
    public Guid ArtistId { get; set; }
    public Guid? ContractorId { get; set; }
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
    public string GuestName { get; set; } = string.Empty;
    public string? GuestCompanyName { get; set; }
    public string GuestPhone { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Requested;

    /// <summary>Solicitação aberta pela rota pública, sem contratante cadastrado.</summary>
    public bool IsGuest => ContractorId is null;

    public Interval ToInterval() => new(
        EventDate.ToDateTime(StartTime, DateTimeKind.Utc),
        EventDate.ToDateTime(EndTime, DateTimeKind.Utc));

    /// <summary>Estados de onde a contratação ainda pode evoluir. Rejeitada/expirada/cancelada são terminais (RN018).</summary>
    public bool CanEvolve =>
        Status is BookingStatus.Requested
            or BookingStatus.Negotiating
            or BookingStatus.Proposed;
}
