using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Rede social do artista exibida na página pública (por enquanto apenas Instagram).
/// O handle é gravado normalizado, sem o "@" — ex.: "dj.peruca".
/// </summary>
public class ArtistSocialLink : BaseEntity
{
    public Guid ArtistId { get; set; }
    public string Platform { get; set; } = "Instagram";
    public string Handle { get; set; } = default!;
}
