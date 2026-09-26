using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Content { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
