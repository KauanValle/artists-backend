using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtistPlatform.Infrastructure.Data;

public class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.ToTable("Artists");
        builder.Property(a => a.Name).HasMaxLength(160).IsRequired();
        builder.Property(a => a.ArtisticName).HasMaxLength(160).IsRequired();
        builder.Property(a => a.City).HasMaxLength(120).IsRequired();
        builder.Property(a => a.State).HasMaxLength(2).IsRequired();
        builder.Property(a => a.Phone).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ProfilePhotoUrl).HasMaxLength(500);
        builder.HasIndex(a => a.UserId).IsUnique();
        builder.HasOne(a => a.Category).WithMany().HasForeignKey(a => a.CategoryId);
    }
}

public class ArtistProfileConfiguration : IEntityTypeConfiguration<ArtistProfile>
{
    public void Configure(EntityTypeBuilder<ArtistProfile> builder)
    {
        builder.ToTable("ArtistProfiles");
        builder.Property(p => p.Slug).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.ArtistId).IsUnique();
    }
}

public class ArtistCategoryConfiguration : IEntityTypeConfiguration<ArtistCategory>
{
    public void Configure(EntityTypeBuilder<ArtistCategory> builder)
    {
        builder.ToTable("ArtistCategories");
        builder.Property(c => c.Name).HasMaxLength(80).IsRequired();
        builder.Property(c => c.Slug).HasMaxLength(80).IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}

public class ArtistAvailabilityConfiguration : IEntityTypeConfiguration<ArtistAvailability>
{
    public void Configure(EntityTypeBuilder<ArtistAvailability> builder)
    {
        builder.ToTable("ArtistAvailabilities");
        builder.Property(a => a.Note).HasMaxLength(500);
        builder.HasIndex(a => new { a.ArtistId, a.Date });
    }
}

public class ArtistFeeConfiguration : IEntityTypeConfiguration<ArtistFee>
{
    public void Configure(EntityTypeBuilder<ArtistFee> builder)
    {
        builder.ToTable("ArtistFees");
        builder.Property(f => f.Price).HasPrecision(12, 2);
        builder.Property(f => f.Description).HasMaxLength(300);
        builder.HasIndex(f => f.ArtistId);
    }
}

public class ContractorConfiguration : IEntityTypeConfiguration<Contractor>
{
    public void Configure(EntityTypeBuilder<Contractor> builder)
    {
        builder.ToTable("Contractors");
        builder.Property(c => c.Name).HasMaxLength(160).IsRequired();
        builder.Property(c => c.CompanyName).HasMaxLength(160);
        builder.Property(c => c.Phone).HasMaxLength(20);
        builder.Property(c => c.City).HasMaxLength(120);
        builder.Property(c => c.State).HasMaxLength(2);
        builder.HasIndex(c => c.UserId).IsUnique();
    }
}

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(300);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.HasIndex(e => new { e.ArtistId, e.StartDateTime });
        builder.HasIndex(e => e.BookingRequestId);
    }
}

public class BookingRequestConfiguration : IEntityTypeConfiguration<BookingRequest>
{
    public void Configure(EntityTypeBuilder<BookingRequest> builder)
    {
        builder.ToTable("BookingRequests");
        builder.Property(b => b.Location).HasMaxLength(300).IsRequired();
        builder.Property(b => b.Message).HasMaxLength(2000);
        builder.Property(b => b.Budget).HasPrecision(12, 2);
        builder.HasIndex(b => new { b.ArtistId, b.Status });
        builder.HasIndex(b => new { b.ContractorId, b.Status });
    }
}

public class ProposalConfiguration : IEntityTypeConfiguration<Proposal>
{
    public void Configure(EntityTypeBuilder<Proposal> builder)
    {
        builder.ToTable("Proposals");
        builder.Property(p => p.TravelCost).HasPrecision(12, 2);
        builder.Property(p => p.EquipmentCost).HasPrecision(12, 2);
        builder.Property(p => p.Discount).HasPrecision(12, 2);
        builder.Property(p => p.FinalAmount).HasPrecision(12, 2);
        builder.Property(p => p.Notes).HasMaxLength(2000);
        builder.HasIndex(p => p.BookingRequestId);
        builder.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.ProposalId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProposalItemConfiguration : IEntityTypeConfiguration<ProposalItem>
{
    public void Configure(EntityTypeBuilder<ProposalItem> builder)
    {
        builder.ToTable("ProposalItems");
        builder.Property(i => i.Description).HasMaxLength(300).IsRequired();
        builder.Property(i => i.Amount).HasPrecision(12, 2);
    }
}

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasIndex(c => c.BookingRequestId).IsUnique();
        builder.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        builder.Property(m => m.Content).HasMaxLength(4000).IsRequired();
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt });
    }
}

public class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("FinancialTransactions");
        builder.Property(t => t.Category).HasMaxLength(80).IsRequired();
        builder.Property(t => t.Amount).HasPrecision(12, 2);
        builder.Property(t => t.Notes).HasMaxLength(2000);
        builder.HasIndex(t => new { t.ArtistId, t.Type });
        builder.HasIndex(t => new { t.ArtistId, t.Status });
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.Property(m => m.Name).HasMaxLength(160).IsRequired();
        builder.Property(m => m.PhotoUrl).HasMaxLength(500);
        builder.Property(m => m.Role).HasMaxLength(80);
        builder.Property(m => m.Phone).HasMaxLength(20);
        builder.Property(m => m.Email).HasMaxLength(160);
        builder.Property(m => m.DefaultShareValue).HasPrecision(12, 2);
        builder.Property(m => m.Notes).HasMaxLength(1000);
        builder.HasIndex(m => m.ArtistId);
    }
}

public class EventTeamShareConfiguration : IEntityTypeConfiguration<EventTeamShare>
{
    public void Configure(EntityTypeBuilder<EventTeamShare> builder)
    {
        builder.ToTable("EventTeamShares");
        builder.Property(s => s.ShareValue).HasPrecision(12, 2);
        builder.HasIndex(s => new { s.EventId, s.TeamMemberId }).IsUnique();
    }
}

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.Property(e => e.Name).HasMaxLength(160).IsRequired();
        builder.Property(e => e.Category).HasMaxLength(80);
        builder.Property(e => e.Brand).HasMaxLength(80);
        builder.Property(e => e.Model).HasMaxLength(80);
        builder.Property(e => e.Identifier).HasMaxLength(80);
        builder.Property(e => e.WeightKg).HasPrecision(8, 2);
        builder.Property(e => e.Value).HasPrecision(12, 2);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.HasIndex(e => e.ArtistId);
    }
}

public class EventEquipmentConfiguration : IEntityTypeConfiguration<EventEquipment>
{
    public void Configure(EntityTypeBuilder<EventEquipment> builder)
    {
        builder.ToTable("EventEquipment");
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => new { x.EventId, x.EquipmentId }).IsUnique();
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasIndex(r => new { r.EventId, r.ContractorId }).IsUnique();
        builder.HasIndex(r => r.ArtistId);
    }
}

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites");
        builder.HasIndex(f => new { f.ContractorId, f.ArtistId }).IsUnique();
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Link).HasMaxLength(300);
        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.Property(t => t.Token).HasMaxLength(200).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique();
        builder.HasIndex(t => t.UserId);
    }
}
