using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Registro de indicação: quando alguém se cadastra usando o link de indicação de um artista.
/// </summary>
public class Referral : BaseEntity
{
    /// <summary>Artista que indicou (dono do código de indicação).</summary>
    public Guid ReferrerArtistId { get; set; }

    /// <summary>Usuário que se cadastrou pelo link de indicação.</summary>
    public Guid ReferredUserId { get; set; }

    /// <summary>Nome do usuário indicado (snapshot no momento do cadastro).</summary>
    public string ReferredUserName { get; set; } = string.Empty;
}
