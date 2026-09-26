using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

public class ArtistCategory : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
