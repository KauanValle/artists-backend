using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Tests;

/// <summary>Detecção de conflitos na agenda (PRD §8) — RN009.</summary>
public class EventConflictTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private (AppDbContext Db, EventService Service) Arrange(Action<AppDbContext>? customize = null)
    {
        var db = TestDb.Create();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = Guid.NewGuid(), ArtistId = Guid.NewGuid() };
        db.Artists.Add(new Artist { Id = user.ArtistId!.Value, UserId = user.UserId!.Value, Name = "Banda X", ArtisticName = "Banda X", City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid() });

        db.Events.Add(new Event
        {
            ArtistId = user.ArtistId.Value,
            Title = "Ensaio",
            Type = EventType.Rehearsal,
            StartDateTime = new DateTime(2026, 10, 10, 14, 0, 0),
            EndDateTime = new DateTime(2026, 10, 10, 17, 0, 0),
            Status = EventStatus.Scheduled
        });

        customize?.Invoke(db);
        db.SaveChanges();

        return (db, new EventService(db, user, new FakeClock(Now)));
    }

    [Fact]
    public async Task Create_ConflitoParcial_LancaRN009()
    {
        var (_, service) = Arrange();

        var request = new CreateEventRequestDto(
            Title: "Show",
            Type: EventType.Show,
            StartDateTime: new DateTime(2026, 10, 10, 16, 0, 0),
            EndDateTime: new DateTime(2026, 10, 10, 19, 0, 0),
            Location: "Casa",
            Description: "");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(request));
        Assert.Contains("RN009", ex.Message);
    }

    [Fact]
    public async Task Create_HorariosAdjacentes_NaoConflita()
    {
        var (db, service) = Arrange();

        var request = new CreateEventRequestDto(
            Title: "Show",
            Type: EventType.Show,
            StartDateTime: new DateTime(2026, 10, 10, 17, 0, 0),
            EndDateTime: new DateTime(2026, 10, 10, 20, 0, 0),
            Location: "Casa",
            Description: "");

        var created = await service.CreateAsync(request);
        Assert.Equal(EventStatus.Scheduled, created.Status);
    }

    [Fact]
    public async Task Create_EventoCanceladoExistente_NaoBloqueia()
    {
        var db = TestDb.Create();
        var artistId = Guid.NewGuid();
        db.Events.Add(new Event
        {
            ArtistId = artistId,
            Title = "Ensaio cancelado",
            Type = EventType.Rehearsal,
            StartDateTime = new DateTime(2026, 10, 10, 14, 0, 0),
            EndDateTime = new DateTime(2026, 10, 10, 17, 0, 0),
            Status = EventStatus.Cancelled
        });
        db.SaveChanges();

        var service = new EventService(db, new FakeCurrentUser { Role = UserRole.Artist, UserId = Guid.NewGuid(), ArtistId = artistId }, new FakeClock(Now));

        var request = new CreateEventRequestDto(
            Title: "Show",
            Type: EventType.Show,
            StartDateTime: new DateTime(2026, 10, 10, 15, 0, 0),
            EndDateTime: new DateTime(2026, 10, 10, 18, 0, 0),
            Location: "Casa",
            Description: "");

        var created = await service.CreateAsync(request);
        Assert.NotEqual(Guid.Empty, created.Id);
    }
}
