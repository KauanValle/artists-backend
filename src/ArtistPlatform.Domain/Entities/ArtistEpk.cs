using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Conteúdo do EPK (Electronic Press Kit) do artista — a página pública /epk/{slug}.
/// Uma linha por artista; campos ainda não configurados ficam vazios e a landing
/// usa o valor do perfil como fallback. Próximas etapas adicionam mais campos
/// (headline, duração, rider, contatos).
/// </summary>
public class ArtistEpk : BaseEntity
{
    public Guid ArtistId { get; set; }

    /// <summary>Descrição exclusiva do EPK — se vazia, usa a descrição do perfil.</summary>
    public string Bio { get; set; } = string.Empty;

    /// <summary>Fotos do kit — se vazia, a landing usa a galeria do perfil.</summary>
    public List<string> PhotoUrls { get; set; } = [];

    /// <summary>Vídeos do kit (MP4 enviado ou link do YouTube) — se vazia, usa os do perfil.</summary>
    public List<string> VideoUrls { get; set; } = [];

    /// <summary>Principais músicas do repertório exibidas no EPK.</summary>
    public List<string> Songs { get; set; } = [];

    public string? SpotifyUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? SoundCloudUrl { get; set; }

    /// <summary>Link da playlist oficial do Spotify exibido no banner do EPK.</summary>
    public string? SpotifyPlaylistUrl { get; set; }
}
