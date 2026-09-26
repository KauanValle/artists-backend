using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Aceite direto da solicitação pelo artista, sem proposta formal (RN007/RN008/RN010/RN017).</summary>
public class BookingDirectAcceptTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private static (AppDbContext Db, FakeCurrentUser User, BookingService Service, Artist Artist, Contractor Contractor, BookingRequest Booking) Seed(
        decimal? budget = 1500m, BookingStatus status = BookingStatus.Requested)
    {
        var db = TestDb.Create();
        var artist = new Artist { UserId = Guid.NewGuid(), Name = "João", ArtisticName = "João Silva", City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid() };
        var contractor = new Contractor { UserId = Guid.NewGuid(), Name = "Maria" };
        var booking = new BookingRequest
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            EventDate = new DateOnly(2026, 10, 10),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Location = "Salão Festas",
            EventType = EventType.Show,
            Budget = budget,
            Status = status
        };
        db.Artists.Add(artist);
        db.Contractors.Add(contractor);
        db.BookingRequests.Add(booking);
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        var service = new BookingService(db, user, new FakeUserDirectory(), new NotificationService(db, user));
        return (db, user, service, artist, contractor, booking);
    }

    private class FakeUserDirectory : IUserDirectory
    {
        public Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(userIds.Distinct().ToDictionary(id => id, id => new UserInfo(id, "Usuário", null)));
    }

    [Fact]
    public async Task Accept_ComOrcamento_CriaEventoReceitaENotifica()
    {
        var (db, _, service, _, contractor, booking) = Seed();

        var result = await service.AcceptAsync(booking.Id);

        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.NotNull(result.EventId);

        var ev = await db.Events.SingleAsync(e => e.BookingRequestId == booking.Id);
        Assert.Equal(EventStatus.Confirmed, ev.Status);
        Assert.True(ev.BlocksAgenda);
        Assert.Equal(contractor.Id, ev.ContractorId);

        // RN010: receita PENDING com o orçamento da solicitação
        var income = await db.FinancialTransactions.SingleAsync(t => t.EventId == ev.Id);
        Assert.Equal(TransactionType.Income, income.Type);
        Assert.Equal(TransactionStatus.Pending, income.Status);
        Assert.Equal(1500m, income.Amount);

        Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Accept_SemOrcamento_Lanca()
    {
        var (db, _, service, _, _, booking) = Seed(budget: null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(booking.Id));
        Assert.Contains("orçamento", ex.Message);
        Assert.Equal(BookingStatus.Requested, booking.Status);
    }

    [Fact]
    public async Task Accept_ComPropostaPendente_Lanca()
    {
        var (db, _, service, artist, contractor, booking) = Seed();
        db.Proposals.Add(new Proposal
        {
            BookingRequestId = booking.Id,
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            Status = ProposalStatus.Pending,
            ValidUntil = Now.AddDays(7),
            FinalAmount = 2000m
        });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(booking.Id));
        Assert.Contains("proposta pendente", ex.Message);
    }

    [Fact]
    public async Task Accept_ConflitoDeAgenda_RN017_Lanca()
    {
        var (db, _, service, artist, _, booking) = Seed();
        db.Events.Add(new Event
        {
            ArtistId = artist.Id,
            Title = "Outro show",
            Type = EventType.Show,
            StartDateTime = new DateTime(2026, 10, 10, 21, 0, 0),
            EndDateTime = new DateTime(2026, 10, 11, 0, 0, 0),
            Status = EventStatus.Confirmed
        });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(booking.Id));
        Assert.Contains("RN017", ex.Message);
    }
}
