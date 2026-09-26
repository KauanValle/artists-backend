using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Ciclo da contratação (PRD §11) e regras RN007/RN008/RN010/RN017/RN018/RN019.</summary>
public class ProposalAcceptanceTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    private static (Artist Artist, Contractor Contractor, BookingRequest Booking, Proposal Proposal) Seed(AppDbContext db)
    {
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
            Status = BookingStatus.Proposed
        };
        var proposal = new Proposal
        {
            BookingRequestId = booking.Id,
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            Status = ProposalStatus.Pending,
            ValidUntil = Now.AddDays(7),
            Items = { new ProposalItem { Description = "Show 3h", Amount = 2000m } },
            TravelCost = 200m,
            EquipmentCost = 300m
        };
        proposal.FinalAmount = proposal.CalculateFinalAmount();

        db.Artists.Add(artist);
        db.Contractors.Add(contractor);
        db.BookingRequests.Add(booking);
        db.Proposals.Add(proposal);
        db.SaveChanges();

        return (artist, contractor, booking, proposal);
    }

    private static ProposalService CreateService(AppDbContext db, FakeCurrentUser user) =>
        new(db, user, new FakeClock(Now), new NotificationService(db, user));

    [Fact]
    public async Task Accept_CriaEventoConfirmado_BloqueiaAgenda_CriaReceitaPending_ENotifica()
    {
        var db = TestDb.Create();
        var (artist, contractor, booking, proposal) = Seed(db);
        var user = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = CreateService(db, user);

        await service.AcceptAsync(proposal.Id);

        // RN007: aceite cria/confirma evento
        var ev = await db.Events.SingleAsync(e => e.BookingRequestId == booking.Id);
        Assert.Equal(EventStatus.Confirmed, ev.Status);
        Assert.Equal(new DateTime(2026, 10, 10, 20, 0, 0), ev.StartDateTime);
        Assert.True(ev.BlocksAgenda); // RN008: evento confirmado bloqueia a agenda

        // solicitação ACCEPTED → CONFIRMED
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(proposal.Id, booking.AcceptedProposalId);

        // RN010: receita PENDING criada com o valor final da proposta (PRD §20: 2000 + 200 + 300)
        var income = await db.FinancialTransactions.SingleAsync(t => t.EventId == ev.Id);
        Assert.Equal(TransactionType.Income, income.Type);
        Assert.Equal(TransactionStatus.Pending, income.Status);
        Assert.Equal(2500m, income.Amount);

        // artista e contratante notificados
        Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Accept_PropostaExpirada_RN019_LancaEMarcaExpired()
    {
        var db = TestDb.Create();
        var (artist, contractor, _, proposal) = Seed(db);
        proposal.ValidUntil = Now.AddDays(-1);
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = CreateService(db, user);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(proposal.Id));

        var stored = await db.Proposals.SingleAsync(p => p.Id == proposal.Id);
        Assert.Equal(ProposalStatus.Expired, stored.Status);
    }

    [Fact]
    public async Task Accept_ConflitoDeAgenda_RN017_Lanca()
    {
        var db = TestDb.Create();
        var (artist, contractor, _, proposal) = Seed(db);
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

        var user = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = CreateService(db, user);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(proposal.Id));
        Assert.Contains("RN017", ex.Message);
    }

    [Fact]
    public async Task Accept_SolicitacaoRejeitada_RN018_Lanca()
    {
        var db = TestDb.Create();
        var (artist, contractor, booking, proposal) = Seed(db);
        booking.Status = BookingStatus.Rejected; // RN018: rejeitada não volta a ser aceita
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = CreateService(db, user);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(proposal.Id));
    }

    [Fact]
    public async Task Accept_ArtistaComBloqueioDeDisponibilidade_RN017_Lanca()
    {
        var db = TestDb.Create();
        var (artist, contractor, booking, proposal) = Seed(db);
        db.ArtistAvailabilities.Add(new ArtistAvailability
        {
            ArtistId = artist.Id,
            Date = booking.EventDate,
            StartTime = new TimeOnly(21, 0),
            EndTime = new TimeOnly(22, 0),
            Status = AvailabilityStatus.Unavailable
        });
        db.SaveChanges();

        var user = new FakeCurrentUser { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = CreateService(db, user);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.AcceptAsync(proposal.Id));
        Assert.Contains("RN017", ex.Message);
    }
}
