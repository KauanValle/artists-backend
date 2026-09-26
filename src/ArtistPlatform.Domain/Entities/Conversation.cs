using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>Uma conversa por contratação (PRD §19).</summary>
public class Conversation : BaseEntity
{
    public Guid BookingRequestId { get; set; }
    public Guid ArtistUserId { get; set; }
    public Guid ContractorUserId { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public List<Message> Messages { get; set; } = [];
}
