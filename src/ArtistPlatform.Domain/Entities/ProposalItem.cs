using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

public class ProposalItem : BaseEntity
{
    public Guid ProposalId { get; set; }
    public string Description { get; set; } = default!;
    public decimal Amount { get; set; }
}
