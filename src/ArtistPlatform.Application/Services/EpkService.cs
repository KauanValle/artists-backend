using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IEpkService
{
    Task<EpkDto> GetByArtistAsync(Guid artistId);
    Task<EpkDto> SaveAsync(Guid userId, SaveEpkRequest request);
}

/// <summary>
/// Conteúdo do EPK (kit de imprensa) do artista. Cada etapa da feature adiciona
/// campos aqui; por ora só a bio específica, que a landing pública usa no lugar
/// da descrição do perfil quando preenchida.
/// </summary>
public class EpkService(IAppDbContext db, ICurrentUserService currentUser) : IEpkService
{
    private const int BioMaxLength = 4000;

    public async Task<EpkDto> GetByArtistAsync(Guid artistId)
    {
        var epk = await db.ArtistEpks.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ArtistId == artistId);
        return new EpkDto(artistId, epk?.Bio ?? string.Empty);
    }

    public async Task<EpkDto> SaveAsync(Guid userId, SaveEpkRequest request)
    {
        var artistId = currentUser.EnsureArtist();

        var bio = (request.Bio ?? string.Empty).Trim();
        if (bio.Length > BioMaxLength)
            throw new ValidationException($"A descrição do EPK deve ter no máximo {BioMaxLength} caracteres.");

        var epk = await db.ArtistEpks.FirstOrDefaultAsync(e => e.ArtistId == artistId);
        if (epk is null)
        {
            epk = new ArtistEpk { ArtistId = artistId };
            db.ArtistEpks.Add(epk);
        }
        epk.Bio = bio;
        epk.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new EpkDto(artistId, epk.Bio);
    }
}
