using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Dados públicos do artista; URL /artistas/{slug} (PRD §15).</summary>
public class ArtistProfile : BaseEntity
{
    public Guid ArtistId { get; set; }
    public string Slug { get; set; } = default!;
    public string Description { get; set; } = string.Empty;
    public List<string> Styles { get; set; } = [];
    public List<string> Specialties { get; set; } = [];
    public List<string> GalleryUrls { get; set; } = [];
    public List<string> VideoUrls { get; set; } = [];
    public bool IsPublished { get; set; }
}
