using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Infrastructure.Data;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Infrastructure.Services;

/// <summary>Consulta nomes/fotos de usuários sobre o Identity + perfis de artista.</summary>
public class UserDirectory(AppDbContext db) : IUserDirectory
{
    public async Task<Dictionary<Guid, UserInfo>> GetUsersAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var users = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.ArtistId })
            .ToListAsync(cancellationToken);

        var artistIds = users.Where(u => u.ArtistId.HasValue).Select(u => u.ArtistId!.Value).ToList();
        var photos = await db.Artists.AsNoTracking()
            .Where(a => artistIds.Contains(a.Id) && !string.IsNullOrEmpty(a.ProfilePhotoUrl))
            .ToDictionaryAsync(a => a.UserId, a => a.ProfilePhotoUrl, cancellationToken);

        return users.ToDictionary(
            u => u.Id,
            u => new UserInfo(u.Id, u.DisplayName, photos.GetValueOrDefault(u.Id)));
    }
}
