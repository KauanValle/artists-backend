using System.Text.Json;
using System.Net;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DomainPushSubscription = ArtistPlatform.Domain.Entities.PushSubscription;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Envia Web Push (VAPID) para todas as inscrições do usuário; remove inscrições inválidas (404/410).</summary>
public class PushNotificationSender(IAppDbContext db, IConfiguration configuration) : IPushNotificationSender
{
    public async Task SendToUserAsync(Guid userId, string title, string message, string? link)
    {
        var publicKey = configuration["Vapid:PublicKey"];
        var privateKey = configuration["Vapid:PrivateKey"];
        var subject = configuration["Vapid:Subject"];
        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(subject))
            return;

        var subscriptions = await db.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();
        if (subscriptions.Count == 0)
            return;

        var mortas = new List<DomainPushSubscription>();
        foreach (var subscription in subscriptions)
        {
            var resultado = await WebPushProtocol.EnviarAsync(
                subscription.Endpoint,
                subscription.P256dh,
                subscription.Auth,
                JsonSerializer.Serialize(new { title, message, link = link ?? "/" }),
                subject,
                publicKey,
                privateKey);

            if (!resultado.Sucesso && resultado.Status is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                mortas.Add(subscription);
        }

        if (mortas.Count > 0)
        {
            db.PushSubscriptions.RemoveRange(mortas);
            await db.SaveChangesAsync();
        }
    }
}
