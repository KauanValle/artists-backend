using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>
/// Conclusão do show em dupla confirmação: artista e contratante precisam confirmar;
/// enquanto falta o contratante, o evento guarda o token do link público de confirmação.
/// </summary>
public class ShowCompletionTests
{
    private class FakeUserDirectory : IUserDirectory
    {
        public Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(userIds.Distinct().ToDictionary(id => id, id => new UserInfo(id, "Usuário", null)));
    }

    private (AppDbContext Db, Artist Artist, Contractor Contractor, BookingRequest Booking) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(), Name = "João", ArtisticName = "DJ Peruca",
            City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid()
        };
        var contractor = new Contractor { UserId = Guid.NewGuid(), Name = "Carlos", Phone = "1198" };
        db.Artists.Add(artist);
        db.Contractors.Add(contractor);
        var booking = new BookingRequest
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            Status = BookingStatus.Confirmed,
            EventDate = new DateOnly(2026, 11, 15),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Location = "Bar do Carlos",
            EventType = EventType.Show,
            Budget = 1500m
        };
        db.BookingRequests.Add(booking);
        db.Events.Add(new Event
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            BookingRequestId = booking.Id,
            Title = "DJ Peruca — Show",
            StartDateTime = booking.EventDate.ToDateTime(booking.StartTime, DateTimeKind.Utc),
            EndDateTime = booking.EventDate.ToDateTime(booking.EndTime, DateTimeKind.Utc),
            Location = booking.Location,
            Status = EventStatus.Confirmed
        });
        booking.EventId = db.Events.Local.First().Id;
        db.SaveChanges();
        return (db, artist, contractor, booking);
    }

    private static BookingService Service(AppDbContext db, FakeCurrentUser user) =>
        new(db, user, new FakeUserDirectory(), new NotificationService(db, user));

    [Fact]
    public async Task Artista_Confirma_PendenteDoContratante_ComToken()
    {
        var (db, artist, contractor, booking) = Seed();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };

        var result = await Service(db, user).CompleteAsync(booking.Id);

        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.True(result.ArtistConfirmed);
        Assert.False(result.ContractorConfirmed);
        Assert.False(string.IsNullOrWhiteSpace(result.ContractorToken));

        var ev = await db.Events.SingleAsync(e => e.Id == booking.EventId);
        Assert.Equal(EventStatus.Confirmed, ev.Status);
        Assert.NotNull(ev.ArtistCompletedAt);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == contractor.UserId));
    }

    [Fact]
    public async Task Contratante_ConfirmaPeloLink_ConcluiShow()
    {
        var (db, artist, _, booking) = Seed();
        var artistUser = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        var primeiro = await Service(db, artistUser).CompleteAsync(booking.Id);

        // Confirmação pública pelo token — sem login.
        var resumo = await Service(db, artistUser).ConfirmCompletionByTokenAsync(primeiro.ContractorToken!);

        Assert.Equal(BookingStatus.Completed, resumo.Status);
        Assert.True(resumo.ArtistConfirmed);
        Assert.True(resumo.ContractorConfirmed);
        var ev = await db.Events.SingleAsync(e => e.Id == booking.EventId);
        Assert.Equal(EventStatus.Completed, ev.Status);
        var bookingAtualizado = await db.BookingRequests.SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Completed, bookingAtualizado.Status);
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.Title == "Show concluído"));
    }

    [Fact]
    public async Task Contratante_Logado_ConfirmaPrimeiro_ArtistaDepois()
    {
        var (db, artist, contractor, booking) = Seed();
        var contractorUser = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };

        var primeiro = await Service(db, contractorUser).CompleteAsync(booking.Id);
        Assert.False(primeiro.ArtistConfirmed);
        Assert.True(primeiro.ContractorConfirmed);

        var artistUser = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        var final = await Service(db, artistUser).CompleteAsync(booking.Id);

        Assert.Equal(BookingStatus.Completed, final.Status);
        // Contratante já tinha confirmado: sem token no retorno.
        Assert.Null(final.ContractorToken);
    }

    [Fact]
    public async Task GetPorToken_RetornaResumoPublico()
    {
        var (db, artist, _, booking) = Seed();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };
        var primeiro = await Service(db, user).CompleteAsync(booking.Id);

        var resumo = await Service(db, user).GetCompletionByTokenAsync(primeiro.ContractorToken!);

        Assert.Equal("DJ Peruca", resumo.ArtistName);
        Assert.Equal("Bar do Carlos", resumo.Location);
        Assert.Equal(new DateOnly(2026, 11, 15), resumo.EventDate);
        Assert.True(resumo.ArtistConfirmed);
        Assert.False(resumo.ContractorConfirmed);
    }

    [Fact]
    public async Task TokenInvalido_Rejeita()
    {
        var (db, artist, _, _) = Seed();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };

        await Assert.ThrowsAsync<NotFoundException>(
            () => Service(db, user).GetCompletionByTokenAsync("nao-existe"));
    }

    [Fact]
    public async Task SolicitacaoNaoConfirmada_RejeitaConclusao()
    {
        var (db, artist, _, booking) = Seed();
        booking.Status = BookingStatus.Requested;
        db.SaveChanges();
        var user = new FakeCurrentUser { Role = UserRole.Artist, UserId = artist.UserId, ArtistId = artist.Id };

        await Assert.ThrowsAsync<BusinessRuleException>(() => Service(db, user).CompleteAsync(booking.Id));
    }
}
