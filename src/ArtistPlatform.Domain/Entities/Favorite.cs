using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Contratante favorita artista (PRD §22).</summary>
public class Favorite : BaseEntity
{
    public Guid ContractorId { get; set; }
    public Guid ArtistId { get; set; }
}
