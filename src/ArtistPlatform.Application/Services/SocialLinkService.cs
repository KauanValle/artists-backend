using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ArtistPlatform.Application.Services;

public interface ISocialLinkService
{
    Task<List<SocialLinkDto>> ListByArtistAsync(Guid artistId);
    /// <summary>Salva (ou remove, com handle vazio) a rede social do artista logado.</summary>
    Task<SocialLinkDto?> SaveAsync(Guid userId, SaveSocialLinkRequest request);
}

/// <summary>
/// Redes sociais do artista exibidas na página pública. Por enquanto apenas o
/// Instagram: o artista informa o @ (ou cola o link do perfil) e o handle é
/// normalizado sem o "@" para montar a URL.
/// </summary>
public partial class SocialLinkService(IAppDbContext db, ICurrentUserService currentUser) : ISocialLinkService
{
    private const string Instagram = "Instagram";

    // Usuário do Instagram: letras, números, ponto e underline, até 30 caracteres.
    [GeneratedRegex("^[A-Za-z0-9._]{1,30}$")]
    private static partial Regex InstagramHandleRegex();

    public async Task<List<SocialLinkDto>> ListByArtistAsync(Guid artistId)
    {
        var links = await db.ArtistSocialLinks.AsNoTracking()
            .Where(l => l.ArtistId == artistId)
            .OrderBy(l => l.Platform)
            .ToListAsync();
        return links.Select(ToDto).ToList();
    }

    public async Task<SocialLinkDto?> SaveAsync(Guid userId, SaveSocialLinkRequest request)
    {
        var artistId = currentUser.EnsureArtist();

        var platform = (request.Platform ?? Instagram).Trim();
        if (!string.Equals(platform, Instagram, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Por enquanto apenas o Instagram é suportado.");

        var handle = NormalizeInstagramHandle(request.Handle);

        var existing = await db.ArtistSocialLinks
            .FirstOrDefaultAsync(l => l.ArtistId == artistId && l.Platform == Instagram);

        // Handle vazio remove o vínculo (limpa o link da página pública).
        if (handle is null)
        {
            if (existing is not null)
            {
                db.ArtistSocialLinks.Remove(existing);
                await db.SaveChangesAsync();
            }
            return null;
        }

        if (!InstagramHandleRegex().IsMatch(handle))
            throw new ValidationException("Usuário do Instagram inválido — use apenas letras, números, ponto e underline (até 30 caracteres).");

        if (existing is null)
        {
            existing = new ArtistSocialLink { ArtistId = artistId, Platform = Instagram };
            db.ArtistSocialLinks.Add(existing);
        }
        existing.Handle = handle.ToLowerInvariant();
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ToDto(existing);
    }

    /// <summary>Aceita "@dj.peruca", "dj.peruca" ou a URL do perfil; devolve o handle sem "@" (null se vazio).</summary>
    private static string? NormalizeInstagramHandle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();
        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var segments = value.Split('?', '#')[0]
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            value = segments.Length > 0 ? segments[^1] : string.Empty;
        }
        value = value.TrimStart('@').Trim();
        return value.Length == 0 ? null : value;
    }

    private static SocialLinkDto ToDto(ArtistSocialLink link) =>
        new(link.ArtistId, link.Platform, link.Handle, $"https://instagram.com/{link.Handle}");
}
