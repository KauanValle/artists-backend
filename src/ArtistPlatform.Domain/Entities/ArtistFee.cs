using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Faixa de cachê pública — referência apenas; valor final vem da proposta (RN004–RN006).</summary>
public class ArtistFee : BaseEntity
{
    public Guid ArtistId { get; set; }
    /// <summary>Duração em minutos (ex.: 60 = 1h).</summary>
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
