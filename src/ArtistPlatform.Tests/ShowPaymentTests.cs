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
/// Pagamento obrigatório do show via InfinityPay: checkout apenas para o contratante
/// dono, com valor do banco; webhook confirma pagamento e show de forma idempotente.
/// </summary>
public class ShowPaymentTests
{
    private class FakeInfinityPay : IInfinityPayClient
    {
        public InfinityPayLinkRequest? UltimoPedido { get; private set; }

        public Task<InfinityPayLinkResult> CreateLinkAsync(InfinityPayLinkRequest request)
        {
            UltimoPedido = request;
            return Task.FromResult(new InfinityPayLinkResult(
                $"tx-{request.PaymentRef:N}",
                $"https://checkout.infinitepay.io/kauan-valle-santos/{request.PaymentRef:N}"));
        }
    }

    private static (AppDbContext Db, FakeCurrentUser User, ShowPaymentService Service) SeedComServico(
        out ShowPayment payment, out BookingRequest booking,
        BookingStatus status = BookingStatus.AwaitingPayment, decimal? budget = null, bool comPropostaAceita = true)
    {
        var db = TestDb.Create();
        var artist = new Artist
        {
            UserId = Guid.NewGuid(), Name = "João", ArtisticName = "DJ Peruca",
            City = "SP", State = "SP", Phone = "119", CategoryId = Guid.NewGuid(), TravelTimeMinutes = 60
        };
        var contractor = new Contractor { UserId = Guid.NewGuid(), Name = "Maria" };
        booking = new BookingRequest
        {
            ArtistId = artist.Id,
            ContractorId = contractor.Id,
            EventDate = new DateOnly(2026, 12, 10),
            StartTime = new TimeOnly(20, 0),
            EndTime = new TimeOnly(23, 0),
            Location = "Salão Festas",
            EventType = EventType.Show,
            Budget = budget,
            Status = status
        };
        if (comPropostaAceita)
        {
            var proposal = new Proposal
            {
                BookingRequestId = booking.Id,
                ArtistId = artist.Id,
                ContractorId = contractor.Id,
                Status = ProposalStatus.Accepted,
                ValidUntil = DateTime.UtcNow.AddDays(7)
            };
            proposal.FinalAmount = 2500m;
            booking.AcceptedProposalId = proposal.Id;
            db.Proposals.Add(proposal);
        }
        db.Artists.Add(artist);
        db.Contractors.Add(contractor);
        db.BookingRequests.Add(booking);
        db.SaveChanges();

        var user = new FakeCurrentUser
        { Role = UserRole.Contractor, UserId = contractor.UserId, ContractorId = contractor.Id };
        var service = new ShowPaymentService(db, user, new FakeInfinityPay(), new NotificationService(db, user));
        payment = null!;
        return (db, user, service);
    }

    [Fact]
    public async Task Checkout_UsaValorDaPropostaAceita_DoBanco()
    {
        var (db, user, service) = SeedComServico(out _, out var booking);

        var resultado = await service.StartCheckoutAsync(booking.Id);

        Assert.Contains("checkout.infinitepay.io", resultado.CheckoutUrl);
        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        Assert.Equal(2500m, payment.Amount); // valor da proposta aceita, não do orçamento
        Assert.Equal(ShowPaymentStatus.Pending, payment.Status);
        Assert.False(string.IsNullOrWhiteSpace(payment.CheckoutUrl));
    }

    [Fact]
    public async Task Checkout_SemProposta_UsaOrcamento()
    {
        var (db, user, service) = SeedComServico(out _, out var booking, budget: 1500m, comPropostaAceita: false);

        await service.StartCheckoutAsync(booking.Id);

        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        Assert.Equal(1500m, payment.Amount);
    }

    [Fact]
    public async Task Checkout_DeOutroContratante_Rejeita()
    {
        var (db, _, service) = SeedComServico(out _, out var booking);
        var intruso = new FakeCurrentUser
        { Role = UserRole.Contractor, UserId = Guid.NewGuid(), ContractorId = Guid.NewGuid() };
        var dbIntruso = db;

        var serviceIntruso = new ShowPaymentService(
            dbIntruso, intruso, new FakeInfinityPay(), new NotificationService(dbIntruso, intruso));

        await Assert.ThrowsAsync<ForbiddenException>(() => serviceIntruso.StartCheckoutAsync(booking.Id));
    }

    [Fact]
    public async Task Checkout_StatusErrado_Rejeita()
    {
        var (db, user, service) = SeedComServico(out _, out var booking, status: BookingStatus.Requested);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartCheckoutAsync(booking.Id));
    }

    [Fact]
    public async Task Webhook_Confirma_CriaEventoReceitaENotifica_EIdempotente()
    {
        var (db, user, service) = SeedComServico(out _, out var booking);
        var checkout = await service.StartCheckoutAsync(booking.Id);
        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);

        var body = $$"""{"event":"payment.confirmed","id":"{{payment.TransactionId}}","status":"paid"}""";
        var resultado = await service.HandleWebhookAsync(payment.Id, body);
        Assert.True(resultado.Handled);

        // show confirmado + evento na agenda com buffer de deslocamento (60 min)
        var bookingAtualizado = await db.BookingRequests.SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Confirmed, bookingAtualizado.Status);
        var ev = await db.Events.SingleAsync(e => e.BookingRequestId == booking.Id);
        Assert.Equal(EventStatus.Confirmed, ev.Status);
        Assert.Equal(new DateTime(2026, 12, 10, 19, 0, 0), ev.StartDateTime);

        // receita PENDING com o valor pago
        var income = await db.FinancialTransactions.SingleAsync(t => t.EventId == ev.Id);
        Assert.Equal(2500m, income.Amount);

        // idempotente: reenvio do mesmo webhook não duplica nada
        await service.HandleWebhookAsync(payment.Id, body);
        Assert.Equal(1, await db.Events.CountAsync(e => e.BookingRequestId == booking.Id));
        Assert.Single(await db.FinancialTransactions.ToListAsync());
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.Type == NotificationType.ShowConfirmed));
    }

    [Fact]
    public async Task Webhook_EventoNaoPago_Ignora()
    {
        var (db, user, service) = SeedComServico(out _, out var booking);
        var checkout = await service.StartCheckoutAsync(booking.Id);
        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);

        var resultado = await service.HandleWebhookAsync(payment.Id, """{"event":"checkout.created"}""");

        Assert.False(resultado.Handled);
        var paymentAtualizado = await db.ShowPayments.SingleAsync(p => p.Id == payment.Id);
        Assert.Equal(ShowPaymentStatus.Pending, paymentAtualizado.Status);
        Assert.Equal(BookingStatus.AwaitingPayment, (await db.BookingRequests.SingleAsync(b => b.Id == booking.Id)).Status);
    }

    [Fact]
    public async Task SegundoCheckout_AposPago_Rejeita()
    {
        var (db, user, service) = SeedComServico(out _, out var booking);
        await service.StartCheckoutAsync(booking.Id);
        var payment = await db.ShowPayments.SingleAsync(p => p.BookingRequestId == booking.Id);
        await service.HandleWebhookAsync(payment.Id, """{"event":"payment.confirmed","id":"x","status":"paid"}""");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.StartCheckoutAsync(booking.Id));
    }
}
