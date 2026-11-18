using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Acesso de portaria de um evento: o produtor cria (ex.: "Portão A") e compartilha o código/link com quem fará
/// a leitura dos QR codes. Não exige conta; só enxerga aquele evento e pode ser revogado a qualquer momento.
/// </summary>
public class GateAccess : BaseEntity
{
    public Guid TicketEventId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Código secreto do acesso (12 caracteres, sem ambíguos).</summary>
    public string Code { get; set; } = default!;

    public DateTime? RevokedAt { get; set; }
}
