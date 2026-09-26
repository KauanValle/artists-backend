using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace ArtistPlatform.Infrastructure.Identity;

/// <summary>Usuário do Identity (AspNetUsers) — PRD §26 "User": identidade, autenticação e role.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? ArtistId { get; set; }
    public Guid? ContractorId { get; set; }
}
