using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Solicitação pela rota pública, sem cadastro de contratante (BookingRequest.ContractorId nulo).</summary>
public class GuestBookingTests
{
    private static (AppDbContext Db, FakeCurrentUser User, BookingService Service, Artist Artist) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(),
            Name = "João",
            ArtisticName = "João Silva",
            City = "SP",
            State = "SP",
            Phone = "119",
            CategoryId = Guid.NewGuid()
        };
        db.Artists.Add(artist);
        db.ArtistProfiles.Add(new ArtistProfile { ArtistId = artist.Id, Slug = "joao-silva", IsPublished = true });
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        var service = new BookingService(db, user, new FakeUserDirectory(), new NotificationService(db, user));
        return (db, user, service, artist);
    }

    private class FakeUserDirectory : IUserDirectory
    {
        public Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(userIds.Distinct().ToDictionary(id => id, id => new UserInfo(id, "Usuário", null)));
    }

    private static CreateGuestBookingRequestDto Request(Guid artistId, decimal budget = 1800m) => new(
        artistId,
        GuestName: "Carlos Souza",
        GuestCompanyName: "Bar do Carlos",
        GuestPhone: "11987654321",
        Budget: budget,
        EventDate: new DateOnly(2027, 3, 10),
        StartTime: new TimeOnly(20, 0),
        EndTime: new TimeOnly(23, 0),
        Location: "Bar do Carlos",
        EventType: EventType.Show,
        EstimatedAudience: 120,
        Message: "Show de 3 horas",
        Addons: null);

    [Fact]
    public async Task CreateGuest_ComDadosValidos_CriaSolicitacaoSemContratante()
    {
        var (db, _, service, artist) = Seed();

        var result = await service.CreateGuestAsync(Request(artist.Id));

        Assert.Equal(BookingStatus.Requested, result.Status);
        Assert.Null(result.ContractorId);
        Assert.True(result.IsGuest);
        Assert.Equal("Carlos Souza", result.ContractorName);
        Assert.Equal("Bar do Carlos", result.GuestCompanyName);
        Assert.Equal("11987654321", result.GuestPhone);
        Assert.Equal(1800m, result.Budget);

        var booking = await db.BookingRequests.SingleAsync(b => b.Id == result.Id);
        Assert.Null(booking.ContractorId);
        Assert.True(booking.IsGuest);
        Assert.Equal(0, await db.Conversations.CountAsync()); // visitante não gera conversa interna
        Assert.Equal(1, await db.Notifications.CountAsync()); // somente o artista é notificado
    }

    [Fact]
    public async Task CreateGuest_SemValorDeProposta_Lanca()
    {
        var (db, _, service, artist) = Seed();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateGuestAsync(Request(artist.Id, budget: 0)));
        Assert.Contains("valor da proposta", ex.Message);
        Assert.Equal(0, await db.BookingRequests.CountAsync());
    }

    [Fact]
    public async Task CreateGuest_SemNomeDoResponsavel_Lanca()
    {
        var (db, _, service, artist) = Seed();
        var request = Request(artist.Id) with { GuestName = " " };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateGuestAsync(request));
        Assert.Contains("nome do responsável", ex.Message);
    }

    [Fact]
    public async Task CreateGuest_SemWhatsapp_Lanca()
    {
        var (db, _, service, artist) = Seed();
        var request = Request(artist.Id) with { GuestPhone = "" };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateGuestAsync(request));
        Assert.Contains("WhatsApp", ex.Message);
    }

    [Fact]
    public async Task CreateGuest_ArtistaNaoPublicado_Lanca()
    {
        var (db, _, service, artist) = Seed();
        var profile = await db.ArtistProfiles.SingleAsync(p => p.ArtistId == artist.Id);
        profile.IsPublished = false;
        db.SaveChanges();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateGuestAsync(Request(artist.Id)));
    }

    [Fact]
    public async Task CreateGuest_EmDiaBloqueado_RN009_Lanca()
    {
        var (db, _, service, artist) = Seed();
        // 2027-03-11 é quinta — dia de trabalho declarado pela disponibilidade abaixo.
        db.ArtistAvailabilities.Add(new ArtistAvailability
        {
            ArtistId = artist.Id,
            Date = new DateOnly(2027, 3, 11),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Status = AvailabilityStatus.Available
        });
        db.ArtistAvailabilities.Add(new ArtistAvailability
        {
            ArtistId = artist.Id,
            Date = new DateOnly(2027, 3, 11),
            StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(23, 0),
            Status = AvailabilityStatus.Unavailable
        });
        db.SaveChanges();

        var request = Request(artist.Id) with { EventDate = new DateOnly(2027, 3, 11) };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateGuestAsync(request));

        Assert.Contains("indisponível", ex.Message);
        Assert.Equal(0, await db.BookingRequests.CountAsync());
    }

    [Fact]
    public async Task CreateGuest_ForaDosDiasDeTrabalho_RN009_Lanca()
    {
        var (db, _, service, artist) = Seed();
        // Artista só atende às quintas; a solicitação é para uma quarta-feira.
        db.ArtistAvailabilities.Add(new ArtistAvailability
        {
            ArtistId = artist.Id,
            Date = new DateOnly(2027, 3, 11),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Status = AvailabilityStatus.Available
        });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateGuestAsync(Request(artist.Id)));

        Assert.Contains("dia da semana", ex.Message);
        Assert.Equal(0, await db.BookingRequests.CountAsync());
    }

    /// <summary>RN017: solicitação não pode cair em horário ocupado por evento confirmado na agenda.</summary>
    [Fact]
    public async Task CreateGuest_SobreEventoConfirmado_RN017_Lanca()
    {
        var (db, _, service, artist) = Seed();
        db.Events.Add(new Event
        {
            ArtistId = artist.Id,
            Title = "Show confirmado",
            Type = EventType.Show,
            StartDateTime = new DateTime(2027, 3, 10, 21, 0, 0, DateTimeKind.Utc),
            EndDateTime = new DateTime(2027, 3, 11, 1, 0, 0, DateTimeKind.Utc),
            Status = EventStatus.Confirmed
        });
        db.SaveChanges();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateGuestAsync(Request(artist.Id)));

        Assert.Contains("RN017", ex.Message);
        Assert.Equal(0, await db.BookingRequests.CountAsync());
    }

    [Fact]
    public async Task Accept_SolicitacaoDeVisitante_ConfirmaEventoSemContratante()
    {
        var (db, _, service, artist) = Seed();
        var created = await service.CreateGuestAsync(Request(artist.Id));

        var result = await service.AcceptAsync(created.Id);

        Assert.Equal(BookingStatus.Confirmed, result.Status);
        var ev = await db.Events.SingleAsync(e => e.BookingRequestId == created.Id);
        Assert.Equal(EventStatus.Confirmed, ev.Status);
        Assert.Null(ev.ContractorId);
        var income = await db.FinancialTransactions.SingleAsync(t => t.EventId == ev.Id);
        Assert.Equal(1800m, income.Amount);
        Assert.Equal(2, await db.Notifications.CountAsync()); // criação + aceite, só para o artista
    }
}
