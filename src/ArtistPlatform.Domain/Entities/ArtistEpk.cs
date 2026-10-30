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
}
