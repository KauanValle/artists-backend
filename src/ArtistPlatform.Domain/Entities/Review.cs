using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Avaliação do contratante ao artista (PRD §21, RN014–RN016).</summary>
public class Review : BaseEntity
{
    public Guid ArtistId { get; set; }
    public Guid ContractorId { get; set; }
    public Guid EventId { get; set; }
    /// <summary>1–5.</summary>
    public int OverallRating { get; set; }
    public int Punctuality { get; set; }
    public int Quality { get; set; }
    public int Professionalism { get; set; }
    public int Communication { get; set; }
    public string Comment { get; set; } = string.Empty;

    public static bool IsValidRating(int value) => value is >= 1 and <= 5;
}
