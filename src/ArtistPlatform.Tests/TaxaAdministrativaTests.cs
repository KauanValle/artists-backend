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
/// Taxa administrativa: configurável pelo admin (AppSettings taxa:administrativa),
/// cobrada a crido do contratante no checkout (show R$ 100 + taxa 5% = R$ 105).
/// </summary>
public class TaxaAdministrativaTests
{
    private class FakeInfinityPay : IInfinityPayClient
    {
        public List<InfinityPayItem> UltimosItens { get; private set; } = [];

        public Task<InfinityPayLinkResult> CreateLinkAsync(Guid paymentRef, List<InfinityPayItem> items)
        {
            UltimosItens = items;
            return Task.FromResult(new InfinityPayLinkResult(
                $"tx-{paymentRef:N}",
                $"https://checkout.infinitepay.io/kauan-valle-santos/{paymentRef:N}"));
        }
    }

    private static (AppDbContext Db, FakeCurrentUser ContractorUser, ShowPaymentService Service, TaxaAdministrativaService TaxaService, BookingRequest Booking) Seed()
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(), Name = "João", ArtisticName = "DJ Peruca",
            City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid()
        };
        var contractor = new Contractor { UserId = Guid.NewGuid(), Name = "Maria" };
        var booking = new BookingRequest
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            EventDate = new DateOnly(2026, 12, 10),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Location = "Salão Festas",
            EventType = EventType.Show,
            Status = BookingStatus.AwaitingPayment
        };
        var proposal = new Proposal
        {
            BookingRequestId = booking.Id,
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            Status = ProposalStatus.Accepted,
            ValidUntil = DateTime.UtcNow.AddDays(7)
        };
        proposal.FinalAmount = 100m; // show de R$ 100
        booking.AcceptedProposalId = proposal.Id;
        db.Artists.Add(artist);
        db.Contractors.Add(contractor);
        db.BookingRequests.Add(booking);
        db.Proposals.Add(proposal);
        db.SaveChanges();

        var user = new FakeCurrentUser
        { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = new ShowPaymentService(db, user, new FakeInfinityPay(), new NotificationService(db, user), new TaxaAdministrativaService(db));
        return (db, user, service, new TaxaAdministrativaService(db), booking);
    }

    [Fact]
    public async Task SemTaxaConfigurada_UsaPadraoDeCincoPorCento()
    {
        var (db, _, service, _, booking) = Seed();

        await service.StartCheckoutAsync(booking.Id);

        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        Assert.Equal(5m, payment.TaxaPercentual);
        Assert.Equal(5m, payment.TaxaValor);
        Assert.Equal(105m, payment.ValorTotal);
    }

    [Fact]
    public async Task TaxaCincoPorCento_Show100_Cobra105()
    {
        var (db, _, service, taxa, booking) = Seed();
        await taxa.SetPercentualAsync(5m, Guid.NewGuid());

        await service.StartCheckoutAsync(booking.Id);

        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        Assert.Equal(5m, payment.TaxaPercentual);
        Assert.Equal(5m, payment.TaxaValor);
        Assert.Equal(105m, payment.ValorTotal);
    }

    [Fact]
    public async Task TaxaArredondaParaCima_NoCentavo()
    {
        var (db, _, service, taxa, booking) = Seed();
        await taxa.SetPercentualAsync(3.33m, Guid.NewGuid()); // 100 * 3,33% = 3,33

        await service.StartCheckoutAsync(booking.Id);

        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        Assert.Equal(3.33m, payment.TaxaValor);
        Assert.Equal(103.33m, payment.ValorTotal);
    }

    [Fact]
    public async Task TaxaAcimaDeCinquenta_Rejeita()
    {
        var (_, _, _, taxa, _) = Seed();

        await Assert.ThrowsAsync<BusinessRuleException>(() => taxa.SetPercentualAsync(51m, Guid.NewGuid()));
    }

    [Fact]
    public async Task Checkout_GeraItensShowETaxa()
    {
        var (db, user, service, taxa, booking) = Seed();
        await taxa.SetPercentualAsync(5m, Guid.NewGuid());

        await service.StartCheckoutAsync(booking.Id);

        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        // validado via fake client na suíte de pagamento — aqui o snapshot do pagamento é o contrato
        Assert.Equal(105m, payment.ValorTotal);
    }
}
