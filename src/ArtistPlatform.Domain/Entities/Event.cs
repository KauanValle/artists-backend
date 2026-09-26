using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Evento da agenda (show, ensaio, reunião, viagem, gravação, outro) — PRD §8.</summary>
public class Event : BaseEntity
{
    public Guid ArtistId { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? BookingRequestId { get; set; }
    public string Title { get; set; } = default!;
    public EventType Type { get; set; } = EventType.Show;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EventStatus Status { get; set; } = EventStatus.Scheduled;

    public Interval ToInterval() => new(StartDateTime, EndDateTime);

    /// <summary>Eventos não cancelados ocupam a agenda (RN008/RN009).</summary>
    public bool BlocksAgenda => Status is EventStatus.Scheduled or EventStatus.Confirmed;

    public static bool ConflictsWith(Event a, Event b) =>
        a.BlocksAgenda && b.BlocksAgenda && a.ToInterval().Overlaps(b.ToInterval());
}
