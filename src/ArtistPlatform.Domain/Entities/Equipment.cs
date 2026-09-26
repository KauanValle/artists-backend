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
    public string Notes { get; set; } = string.Empty;
}
