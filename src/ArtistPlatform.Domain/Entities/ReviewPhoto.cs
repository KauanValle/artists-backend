using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Foto anexada a uma avaliação (upload no storage, ordenada).</summary>
public class ReviewPhoto : BaseEntity
{
    public Guid ReviewId { get; set; }
    public string Url { get; set; } = default!;
    public int Order { get; set; }
}
