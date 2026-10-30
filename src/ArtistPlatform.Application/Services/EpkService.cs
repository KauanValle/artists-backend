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
    private const int MaxPhotos = 12;
    private const int MaxVideos = 8;
    private const int UrlMaxLength = 500;

    public async Task<EpkDto> GetByArtistAsync(Guid artistId)
    {
        var epk = await db.ArtistEpks.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ArtistId == artistId);
        return new EpkDto(artistId, epk?.Bio ?? string.Empty, epk?.PhotoUrls ?? [], epk?.VideoUrls ?? []);
    }

    public async Task<EpkDto> SaveAsync(Guid userId, SaveEpkRequest request)
    {
        var artistId = currentUser.EnsureArtist();

        var bio = (request.Bio ?? string.Empty).Trim();
        if (bio.Length > BioMaxLength)
            throw new ValidationException($"A descrição do EPK deve ter no máximo {BioMaxLength} caracteres.");

        var photos = SanitizeUrls(request.PhotoUrls);
        var videos = SanitizeUrls(request.VideoUrls);
        if (photos.Count > MaxPhotos)
            throw new ValidationException($"O EPK aceita no máximo {MaxPhotos} fotos.");
        if (videos.Count > MaxVideos)
            throw new ValidationException($"O EPK aceita no máximo {MaxVideos} vídeos.");

        var epk = await db.ArtistEpks.FirstOrDefaultAsync(e => e.ArtistId == artistId);
        if (epk is null)
        {
            epk = new ArtistEpk { ArtistId = artistId };
            db.ArtistEpks.Add(epk);
        }
        epk.Bio = bio;
        epk.PhotoUrls = photos;
        epk.VideoUrls = videos;
        epk.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new EpkDto(artistId, epk.Bio, epk.PhotoUrls, epk.VideoUrls);
    }

    /// <summary>Remove vazios/duplicatas, corta espaços e valida o formato das URLs de mídia.</summary>
    private static List<string> SanitizeUrls(List<string>? urls)
    {
        if (urls is null || urls.Count == 0)
            return [];

        var clean = new List<string>();
        foreach (var raw in urls)
        {
            var url = raw?.Trim() ?? string.Empty;
            if (url.Length == 0)
                continue;
            if (url.Length > UrlMaxLength)
                throw new ValidationException($"URL de mídia muito longa (máximo {UrlMaxLength} caracteres).");
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute)
                && !url.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Uma das URLs de mídia é inválida — informe um link http(s) ou um arquivo enviado.");
            if (!clean.Contains(url))
                clean.Add(url);
        }
        return clean;
    }
}
