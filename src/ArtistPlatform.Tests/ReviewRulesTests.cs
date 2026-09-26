using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Tests;

/// <summary>Regras de avaliação (PRD §21): RN014, RN015.</summary>
public class ReviewRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private (AppDbContext Db, FakeCurrentUser User, ReviewService Service, Artist Artist, Contractor Contractor, Event Ev) Arrange(
        DateTime? eventStart = null, EventStatus eventStatus = EventStatus.Completed)
    {
        var db = TestDb.Create();
        var user = new FakeCurrentUser { Role = UserRole.Contractor };

        var artist = new Artist { UserId = Guid.NewGuid(), Name = "João", ArtisticName = "João Silva", City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid() };
        var contractor = new Contractor { UserId = Guid.NewGuid(), Name = "Maria" };
        db.Artists.Add(artist);
        db.Contractors.Add(contractor);

        var ev = new Event
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            Title = "Show",
            Type = EventType.Show,
            StartDateTime = eventStart ?? Now.AddDays(-7),
            EndDateTime = (eventStart ?? Now.AddDays(-7)).AddHours(3),
            Status = eventStatus
        };
        db.Events.Add(ev);
        db.SaveChanges();

        user.UserId = contractor.UserId;
        user.ContractorId = contractor.Id;

        var service = new ReviewService(db, user, new FakeClock(Now), new NotificationService(db, user));
        return (db, user, service, artist, contractor, ev);
    }

    private static CreateReviewRequestDto ValidRequest(Guid eventId) => new(
        EventId: eventId,
        OverallRating: 5,
        Punctuality: 5,
        Quality: 4,
        Professionalism: 5,
        Communication: 4,
        Comment: "Ótimo show!");

    [Fact]
    public async Task Create_AposEvento_CriaAvaliacaoENotifica()
    {
        var (db, _, service, _, _, ev) = Arrange();

        var review = await service.CreateAsync(ValidRequest(ev.Id));

        Assert.Equal(5, review.OverallRating);
        Assert.Equal(ev.ArtistId, review.ArtistId);
        Assert.Single(await Task.FromResult(db.Reviews.ToList()));
    }

    [Fact]
    public async Task Create_AntesDaDataDoEvento_RN015_Lanca()
    {
        var (_, _, service, _, _, ev) = Arrange(eventStart: Now.AddDays(3), eventStatus: EventStatus.Confirmed);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ValidRequest(ev.Id)));
        Assert.Contains("RN015", ex.Message);
    }

    [Fact]
    public async Task Create_NaoParticipante_RN014_Lanca()
    {
        var (db, user, service, _, _, ev) = Arrange();

        var outsider = new Contractor { UserId = Guid.NewGuid(), Name = "Pedro" };
        db.Contractors.Add(outsider);
        db.SaveChanges();

        user.UserId = outsider.UserId;
        user.ContractorId = outsider.Id;

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(ValidRequest(ev.Id)));
        Assert.Contains("RN014", ex.Message);
    }

    [Fact]
    public async Task Create_NotaInvalida_Lanca()
    {
        var (_, _, service, _, _, ev) = Arrange();

        var request = ValidRequest(ev.Id) with { OverallRating = 6 };
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(request));
    }
}
