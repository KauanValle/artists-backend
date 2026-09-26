using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Checklist de equipamento por evento (PRD §14).</summary>
public class EventEquipment : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid EquipmentId { get; set; }
    public bool IsChecked { get; set; }
    public string Notes { get; set; } = string.Empty;
}
