using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArtistPlatform.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<Identity.AppUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<ArtistProfile> ArtistProfiles => Set<ArtistProfile>();
    public DbSet<ArtistCategory> ArtistCategories => Set<ArtistCategory>();
    public DbSet<ArtistAvailability> ArtistAvailabilities => Set<ArtistAvailability>();
    public DbSet<ArtistFee> ArtistFees => Set<ArtistFee>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalItem> ProposalItems => Set<ProposalItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<EventTeamShare> EventTeamShares => Set<EventTeamShare>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EventEquipment> EventEquipment => Set<EventEquipment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Npgsql mapeia DateOnly→date e TimeOnly→time nativamente; List<string> vai como texto JSON.
        configurationBuilder.Properties<List<string>>()
            .HaveConversion<StringListConverter, StringListComparer>()
            .HaveMaxLength(2000);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

public class StringListConverter : ValueConverter<List<string>, string>
{
    public StringListConverter() : base(
        list => System.Text.Json.JsonSerializer.Serialize(list),
        json => string.IsNullOrWhiteSpace(json)
            ? new List<string>()
            : System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
    {
    }
}

public class StringListComparer : ValueComparer<List<string>>
{
    public StringListComparer() : base(
        (a, b) => a.SequenceEqual(b),
        a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
        a => a.ToList())
    {
    }
}
