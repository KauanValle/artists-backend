using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

public class Equipment : BaseEntity
{
    public Guid ArtistId { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public decimal? WeightKg { get; set; }
    public decimal? Value { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;

    /// <summary>Interno = uso exclusivo do artista; Externo = adicional que o contratante pode contratar com o show.</summary>
    public AllocationType AllocationType { get; set; } = AllocationType.Interno;

    /// <summary>Custo do adicional sobre a proposta: porcentagem do cachê ou valor fixo.</summary>
    public ShareType ShareType { get; set; } = ShareType.Fixed;
    public decimal ShareValue { get; set; }

    public string Notes { get; set; } = string.Empty;
}
