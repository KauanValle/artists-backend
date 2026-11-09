using System.Net;
using System.Text.Json;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DomainPushSubscription = ArtistPlatform.Domain.Entities.PushSubscription;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Envia Web Push (VAPID) para todas as inscrições do usuário; remove inscrições inválidas (404/410).</summary>
public class PushNotificationSender(IAppDbContext db, IConfiguration configuration, ILogger<PushNotificationSender> logger) : IPushNotificationSender
{
    public async Task SendToUserAsync(Guid userId, string title, string message, string? link)
    {
        var publicKey = configuration["Vapid:PublicKey"];
        var privateKey = configuration["Vapid:PrivateKey"];
        var subject = configuration["Vapid:Subject"];
        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(subject))
        {
            logger.LogWarning("Push ignorado: configuração Vapid (Subject/PublicKey/PrivateKey) incompleta.");
            return;
        }

        var subscriptions = await db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();
        if (subscriptions.Count == 0)
            return;

        var payload = JsonSerializer.Serialize(new { title, message, link = link ?? "/" });
        var mortas = new List<DomainPushSubscription>();
        foreach (var subscription in subscriptions)
        {
            var resultado = await WebPushProtocol.EnviarAsync(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth,
                payload,
                subject,
                publicKey,
                privateKey);

            if (resultado.Sucesso)
                continue;

            // 404/410: a inscrição expirou ou foi revogada no navegador — remove do banco.
            if (resultado.Status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                mortas.Add(subscription);
            else
                logger.LogWarning("Falha ao enviar push (status {Status}): {Erro}", resultado.Status, resultado.Erro);
        }

        if (mortas.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(mortas);
            await db.SaveChangesAsync();
        }
    }
}
