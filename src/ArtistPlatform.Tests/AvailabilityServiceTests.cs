using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;

namespace ArtistPlatform.Tests;

/// <summary>Criação de disponibilidade com checagem de conflito de agenda (RN009).</summary>
public class AvailabilityServiceTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private static (AppDbContext Db, FakeCurrentUser User, AvailabilityService Service) Arrange(
        Guid artistId, Action<AppDbContext>? customize = null)
    {
        var db = TestDb.Create();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = Guid.NewGuid(), ArtistId = artistId };
        db.Artists.Add(new Artist
        {
            Id = artistId,
            UserId = user.UserId!.Value,
            Name = "João",
            ArtisticName = "João Silva",
            City = "SP",
            State = "SP",
            Phone = "119",
            CategoryId = Guid.NewGuid()
        });
        customize?.Invoke(db);
        db.SaveChanges();
        return (db, user, new AvailabilityService(db, user));
    }

    private static SaveAvailabilityRequest Request(DateOnly date, string start, string end) => new(
        Date: date,
        StartTime: TimeOnly.Parse(start),
        EndTime: TimeOnly.Parse(end),
        IsAllDay: false,
        Status: AvailabilityStatus.Available,
        Note: null);

    [Fact]
    public async Task Create_DiaLivre_CriaIntervalo()
    {
        var (db, user, service) = Arrange(Guid.NewGuid());

        var rows = await service.CreateAsync(Guid.Empty, Request(new DateOnly(2026, 12, 20), "18:00", "23:00"));

        Assert.Single(rows);
        Assert.Equal(user.ArtistId, rows[0].ArtistId);
    }

    /// <summary>Regressão: a checagem de conflito com eventos precisa ser traduzível pelo EF
    /// (status explícitos em vez da propriedade computada BlocksAgenda).</summary>
    [Fact]
    public async Task Create_ConflitoComEventoConfirmado_RN009_Lanca()
    {
        var artistId = Guid.NewGuid();
        var (db, user, service) = Arrange(artistId, db =>
        {
            db.Events.Add(new Event
            {
                ArtistId = artistId,
                Title = "Show confirmado",
                Type = EventType.Show,
                StartDateTime = new DateTime(2026, 12, 20, 20, 0, 0),
                EndDateTime = new DateTime(2026, 12, 20, 23, 0, 0),
                Status = EventStatus.Confirmed
            });
        });

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(Guid.Empty, Request(new DateOnly(2026, 12, 20), "21:00", "22:00")));
        Assert.Contains("RN009", ex.Message);
    }

    [Fact]
    public async Task Create_ConflitoComEventoCancelado_Permite()
    {
        var artistId = Guid.NewGuid();
        var (db, user, service) = Arrange(artistId, db =>
        {
            db.Events.Add(new Event
            {
                ArtistId = artistId,
                Title = "Ensaio cancelado",
                Type = EventType.Rehearsal,
                StartDateTime = new DateTime(2026, 12, 20, 20, 0, 0),
                EndDateTime = new DateTime(2026, 12, 20, 23, 0, 0),
                Status = EventStatus.Cancelled
            });
        });

        var rows = await service.CreateAsync(Guid.Empty, Request(new DateOnly(2026, 12, 20), "21:00", "22:00"));
        Assert.Single(rows);
    }

    [Fact]
    public async Task Create_SobreposicaoComOutroBloqueio_RN009_Lanca()
    {
        var artistId = Guid.NewGuid();
        var (db, user, service) = Arrange(artistId, db =>
        {
            db.ArtistAvailabilities.Add(new ArtistAvailability
            {
                ArtistId = artistId,
                Date = new DateOnly(2026, 12, 20),
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(12, 0),
                Status = AvailabilityStatus.Unavailable
            });
        });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(Guid.Empty, Request(new DateOnly(2026, 12, 20), "11:00", "13:00")));
    }
}
