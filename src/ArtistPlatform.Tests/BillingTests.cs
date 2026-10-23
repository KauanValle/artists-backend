using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Tests;

/// <summary>Ciclo da assinatura: checkout salvo na tabela Billings e webhook PAYMENT_CONFIRMED promovendo a conta.</summary>
public class BillingTests
{
    private class FakeAsaasClient : IAsaasCheckoutClient
    {
        public CheckoutResponseDto Response { get; set; } = new("sessao-1", "https://sandbox.asaas.com/checkout/sessao-1", "ACTIVE");

        public Task<CheckoutResponseDto> CreateCheckoutAsync(Guid userId)
        {
            CalledWithUserId = userId;
            return Task.FromResult(Response);
        }

        public Guid? CalledWithUserId { get; private set; }
    }

    private class FakeSubscriptionActivator : ISubscriptionActivator
    {
        public List<Guid> ActivatedUsers { get; } = [];

        public Task<bool> ActivateAsync(Guid userId)
        {
            ActivatedUsers.Add(userId);
            return Task.FromResult(true);
        }
    }

    private static (AppDbContext Db, FakeAsaasClient Asaas, FakeSubscriptionActivator Activator, BillingService Service) Setup()
    {
        var db = TestDb.Create();
        var asaas = new FakeAsaasClient();
        var activator = new FakeSubscriptionActivator();
        var user = new FakeCurrentUser { Role = UserRole.Admin, UserId = Guid.NewGuid() };
        var service = new BillingService(db, asaas, activator, new NotificationService(db, user));
        return (db, asaas, activator, service);
    }

    [Fact]
    public async Task CreateCheckout_SalvaBillingComIdLinkStatus()
    {
        var (db, asaas, _, service) = Setup();
        var userId = Guid.NewGuid();

        var result = await service.CreateCheckoutAsync(userId);

        Assert.Equal(userId, asaas.CalledWithUserId);
        var billing = await db.Billings.SingleAsync(b => b.UserId == userId);
        Assert.Equal(result.CheckoutSessionId, billing.CheckoutSessionId);
        Assert.Equal("https://sandbox.asaas.com/checkout/sessao-1", billing.Link);
        Assert.Equal("ACTIVE", billing.Status);
    }

    [Fact]
    public async Task HandleWebhook_PaymentConfirmed_PromoveContaParaAssinante()
    {
        var (db, _, activator, service) = Setup();
        var userId = Guid.NewGuid();
        db.Billings.Add(new Billing { UserId = userId, CheckoutSessionId = "sessao-9", Link = "https://x", Status = "ACTIVE" });
        db.SaveChanges();

        var payload = """
            {
              "event": "PAYMENT_CONFIRMED",
              "payment": { "id": "pay_1", "value": 49.9 },
              "checkoutSession": "sessao-9"
            }
            """;

        var result = await service.HandleWebhookAsync(payload);

        Assert.True(result.Handled);
        Assert.Equal([userId], activator.ActivatedUsers);
        var billing = await db.Billings.SingleAsync(b => b.CheckoutSessionId == "sessao-9");
        Assert.Equal("CONFIRMED", billing.Status);
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == userId));
    }

    [Fact]
    public async Task HandleWebhook_SubscriptionCreatedComSessionAninhada_PromoveConta()
    {
        var (db, _, activator, service) = Setup();
        var userId = Guid.NewGuid();
        db.Billings.Add(new Billing { UserId = userId, CheckoutSessionId = "91d00863-a43b-415f-9ff5-df0a0138bcec", Link = "https://x", Status = "ACTIVE" });
        db.SaveChanges();

        // Formato real do payload do Asaas: checkoutSession aninhado em subscription.
        var payload = """
            {
              "id": "evt_6561b631fa5580caadd00bbe3b858607&23160416",
              "event": "SUBSCRIPTION_CREATED",
              "dateCreated": "2026-10-22 23:56:20",
              "subscription": {
                "object": "subscription",
                "id": "sub_ejas3q20j2lywvcy",
                "customer": "cus_000009648222",
                "value": 49.9,
                "cycle": "MONTHLY",
                "billingType": "CREDIT_CARD",
                "status": "ACTIVE",
                "checkoutSession": "91d00863-a43b-415f-9ff5-df0a0138bcec"
              }
            }
            """;

        var result = await service.HandleWebhookAsync(payload);

        Assert.True(result.Handled);
        Assert.Equal([userId], activator.ActivatedUsers);
        var billing = await db.Billings.SingleAsync(b => b.CheckoutSessionId == "91d00863-a43b-415f-9ff5-df0a0138bcec");
        Assert.Equal("CONFIRMED", billing.Status);
    }

    [Fact]
    public async Task HandleWebhook_PaymentConfirmadoComSessionNoPayment_PromoveConta()
    {
        var (db, _, activator, service) = Setup();
        var userId = Guid.NewGuid();
        db.Billings.Add(new Billing { UserId = userId, CheckoutSessionId = "sessao-payment", Link = "https://x", Status = "ACTIVE" });
        db.SaveChanges();

        var payload = """{ "event": "PAYMENT_CONFIRMED", "payment": { "id": "pay_1", "checkoutSession": "sessao-payment" } }""";

        var result = await service.HandleWebhookAsync(payload);

        Assert.True(result.Handled);
        Assert.Equal([userId], activator.ActivatedUsers);
    }

    [Fact]
    public async Task HandleWebhook_EventoDiferente_Ignora()
    {
        var (db, _, activator, service) = Setup();

        var result = await service.HandleWebhookAsync("""{ "event": "PAYMENT_REFUNDED", "checkoutSession": "s" }""");

        Assert.False(result.Handled);
        Assert.Empty(activator.ActivatedUsers);
        Assert.Equal(0, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task HandleWebhook_SessaoDesconhecida_NaoPromoveNinguem()
    {
        var (_, _, activator, service) = Setup();

        var result = await service.HandleWebhookAsync(
            """{ "event": "PAYMENT_CONFIRMED", "checkoutSession": "sessao-inexistente" }""");

        Assert.False(result.Handled);
        Assert.Empty(activator.ActivatedUsers);
    }

    [Fact]
    public async Task HandleWebhook_JsonInvalido_RetornaSemLancar()
    {
        var (_, _, activator, service) = Setup();

        var result = await service.HandleWebhookAsync("isto não é json");

        Assert.False(result.Handled);
        Assert.Empty(activator.ActivatedUsers);
    }
}
