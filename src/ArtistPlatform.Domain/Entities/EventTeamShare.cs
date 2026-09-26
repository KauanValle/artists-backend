using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Sobrescrita da divisão de cachê por evento (RN013).</summary>
public class EventTeamShare : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid TeamMemberId { get; set; }
    public ShareType ShareType { get; set; } = ShareType.Percentage;
    public decimal ShareValue { get; set; }
}
