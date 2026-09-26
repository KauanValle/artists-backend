namespace ArtistPlatform.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Intervalo de tempo para checagem de conflitos (RN009 / RN020).</summary>
public record Interval(DateTime Start, DateTime End)
{
    public bool Overlaps(Interval other) =>
        Start < other.End && other.Start < End;
}
