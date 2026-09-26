using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Disponibilidade por dia, com múltiplos intervalos por dia (PRD §8/§9).
/// IsAllDay = dia inteiro (00:00–23:59).
/// </summary>
public class ArtistAvailability : BaseEntity
{
    public Guid ArtistId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Available;
    public string? Note { get; set; }

    public Interval ToInterval() => new(
        Date.ToDateTime(StartTime),
        Date.ToDateTime(IsAllDay ? new TimeOnly(23, 59, 59) : EndTime));
}
