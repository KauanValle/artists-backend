using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Artista/banda operacional (RN001: um usuário possui um perfil principal de artista).</summary>
public class Artist : BaseEntity
{
    public Guid UserId { get; set; }
    public ArtistType Type { get; set; } = ArtistType.Solo;
    public string Name { get; set; } = default!;
    public string ArtisticName { get; set; } = default!;
    public Guid CategoryId { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string ProfilePhotoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Tempo de deslocamento/montagem/desmontagem (minutos) reservado antes e depois
    /// de cada show confirmado na agenda.
    /// </summary>
    public int TravelTimeMinutes { get; set; }

    public ArtistCategory Category { get; set; } = default!;
}
