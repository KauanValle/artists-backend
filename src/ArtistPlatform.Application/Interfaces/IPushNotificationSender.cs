using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Entities;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>Persistência das inscrições push do PWA.</summary>
public interface IPushSubscriptionService
{
    Task SalvarAsync(Guid userId, string endpoint, string p256dh, string auth);
    Task RemoverAsync(string endpoint);
}

/// <summary>Notificação push Web Push (VAPID) para os dispositivos do usuário.</summary>
public interface IPushNotificationSender
{
    /// <summary>Envia para todas as inscrições push do usuário; falhas são silenciosas (best-effort).</summary>
    Task SendToUserAsync(Guid userId, string title, string message, string? link);
}
