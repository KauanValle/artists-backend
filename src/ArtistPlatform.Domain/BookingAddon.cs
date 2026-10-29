using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Adicional escolhido pelo contratante na solicitação (equipe externa ou equipamento externo).
/// Nome, valores e quantia são snapshot no momento da contratação.
/// </summary>
public class BookingAddon : BaseEntity
{
    public Guid BookingRequestId { get; set; }
    public AddonItemType ItemType { get; set; }
    public Guid ItemId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Percentage = percentual sobre o cachê base; Fixed = valor fixo somado.</summary>
    public ShareType ShareType { get; set; }
    public decimal ShareValue { get; set; }

    /// <summary>Valor calculado do adicional sobre a proposta base.</summary>
    public decimal Amount { get; set; }
}
