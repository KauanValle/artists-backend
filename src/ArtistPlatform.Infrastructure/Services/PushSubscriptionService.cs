using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Persistência das inscrições push do PWA.</summary>
public class PushSubscriptionService(IAppDbContext db) : IPushSubscriptionService
{
    public async Task SalvarAsync(Guid userId, string endpoint, string p256dh, string auth)
    {
        var existente = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint);
        if (existente is null)
        {
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = userId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth
            });
        }
        else
        {
            existente.P256dh = p256dh;
            existente.Auth = auth;
            existente.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task RemoverAsync(string endpoint)
    {
        var existentes = await db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ToListAsync();
        if (existentes.Count == 0) return;
        db.PushSubscriptions.RemoveRange(existentes);
        await db.SaveChangesAsync();
    }
}
