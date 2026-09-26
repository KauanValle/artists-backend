using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IFavoriteService
{
    Task<List<FavoriteDto>> ListAsync();
    Task AddAsync(Guid artistId);
    Task RemoveAsync(Guid artistId);
}

/// <summary>Favoritos do contratante (PRD §22).</summary>
public class FavoriteService(IAppDbContext db, ICurrentUserService currentUser) : IFavoriteService
{
    public async Task<List<FavoriteDto>> ListAsync()
    {
        var contractorId = currentUser.EnsureContractor();

        var favorites = await db.Favorites.AsNoTracking()
            .Where(f => f.ContractorId == contractorId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();
        var artistIds = favorites.Select(f => f.ArtistId).ToList();

        var artists = await db.Artists.AsNoTracking()
            .Where(a => artistIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.ArtisticName, a.ProfilePhotoUrl, a.CategoryId, a.City });
        var categoryIds = artists.Values.Select(a => a.CategoryId).Distinct().ToList();
        var categories = await db.ArtistCategories.AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var slugs = await db.ArtistProfiles.AsNoTracking()
            .Where(p => artistIds.Contains(p.ArtistId))
            .ToDictionaryAsync(p => p.ArtistId, p => p.Slug);

        return favorites.Select(f =>
        {
            var artist = artists.GetValueOrDefault(f.ArtistId);
            return new FavoriteDto(
                f.Id, f.ArtistId, artist?.ArtisticName ?? "Artista", artist?.ProfilePhotoUrl,
                slugs.GetValueOrDefault(f.ArtistId, string.Empty),
                artist is null ? string.Empty : categories.GetValueOrDefault(artist.CategoryId, string.Empty),
                artist?.City ?? string.Empty);
        }).ToList();
    }

    public async Task AddAsync(Guid artistId)
    {
        var contractorId = currentUser.EnsureContractor();

        var exists = await db.Artists.AnyAsync(a => a.Id == artistId);
        if (!exists)
            throw new NotFoundException("Artista não encontrado.");

        var already = await db.Favorites.AnyAsync(f => f.ContractorId == contractorId && f.ArtistId == artistId);
        if (already) return;

        db.Favorites.Add(new Favorite { ContractorId = contractorId, ArtistId = artistId });
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid artistId)
    {
        var contractorId = currentUser.EnsureContractor();
        var favorite = await db.Favorites.FirstOrDefaultAsync(f => f.ContractorId == contractorId && f.ArtistId == artistId);
        if (favorite is null) return;
        db.Favorites.Remove(favorite);
        await db.SaveChangesAsync();
    }
}
