using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Domain.Entities;

public class Contractor : BaseEntity
{
    public Guid UserId { get; set; }
    public ContractorType Type { get; set; } = ContractorType.Person;
    public string Name { get; set; } = default!;
    public string? CompanyName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
}
