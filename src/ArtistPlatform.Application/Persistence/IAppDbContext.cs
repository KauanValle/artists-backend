using Microsoft.EntityFrameworkCore;
using ArtistPlatform.Domain.Entities;

namespace ArtistPlatform.Application.Persistence;

/// <summary>
/// Abstração do banco de dados para a camada de Application (implementada pelo EF Core na Infrastructure).
/// </summary>
public interface IAppDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Artist> Artists { get; }
    DbSet<ArtistProfile> ArtistProfiles { get; }
    DbSet<ArtistCategory> ArtistCategories { get; }
    DbSet<ArtistAvailability> ArtistAvailabilities { get; }
    DbSet<ArtistFee> ArtistFees { get; }
    DbSet<Contractor> Contractors { get; }
    DbSet<Event> Events { get; }
    DbSet<BookingRequest> BookingRequests { get; }
    DbSet<Proposal> Proposals { get; }
    DbSet<ProposalItem> ProposalItems { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<FinancialTransaction> FinancialTransactions { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<EventTeamShare> EventTeamShares { get; }
    DbSet<Equipment> Equipment { get; }
    DbSet<EventEquipment> EventEquipment { get; }
    DbSet<Review> Reviews { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Notification> Notifications { get; }
}
