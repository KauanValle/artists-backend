using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

// ─────────────────────────── DTOs ───────────────────────────

public record ReferralSummaryDto(
    string ReferralCode,
    string ReferralLink,
    int TotalReferrals,
    List<ReferralItemDto> Referrals);

public record ReferralItemDto(
    Guid Id,
    string ReferredUserName,
    DateTime CreatedAt);

// ─────────────────────────── Interface ───────────────────────────

public interface IReferralService
{
    /// <summary>Resumo de indicações do artista logado.</summary>
    Task<ReferralSummaryDto> GetArtistReferralSummaryAsync(string baseUrl);

    /// <summary>Garante que o artista tem um código de indicação (gera se não tiver).</summary>
    Task<string> EnsureReferralCodeAsync(Guid artistId);

    /// <summary>Registra uma indicação quando alguém se cadastra com um código de referência.</summary>
    Task RegisterReferralAsync(string referralCode, Guid referredUserId, string referredUserName);
}

// ─────────────────────────── Implementação ───────────────────────────

public class ReferralService(IAppDbContext db, ICurrentUserService currentUser) : IReferralService
{
    public async Task<ReferralSummaryDto> GetArtistReferralSummaryAsync(string baseUrl)
    {
        var artistId = currentUser.EnsureArtist();

        var artist = await db.Artists.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artistId)
            ?? throw new ApplicationException("Artista não encontrado.");

        // Gera código se ainda não tem
        var code = artist.ReferralCode;
        if (string.IsNullOrEmpty(code))
        {
            code = await EnsureReferralCodeAsync(artistId);
        }

        var referrals = await db.Referrals.AsNoTracking()
            .Where(r => r.ReferrerArtistId == artistId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReferralItemDto(r.Id, r.ReferredUserName, r.CreatedAt))
            .ToListAsync();

        var link = $"{baseUrl.TrimEnd('/')}/register?ref={code}";

        return new ReferralSummaryDto(code, link, referrals.Count, referrals);
    }

    public async Task<string> EnsureReferralCodeAsync(Guid artistId)
    {
        var artist = await db.Artists.FirstOrDefaultAsync(a => a.Id == artistId);
        if (artist is null) throw new ApplicationException("Artista não encontrado.");

        if (!string.IsNullOrEmpty(artist.ReferralCode))
            return artist.ReferralCode;

        // Gera um código curto e único (8 caracteres alfanuméricos)
        string code;
        do
        {
            code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        } while (await db.Artists.AnyAsync(a => a.ReferralCode == code));

        artist.ReferralCode = code;
        artist.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return code;
    }

    public async Task RegisterReferralAsync(string referralCode, Guid referredUserId, string referredUserName)
    {
        if (string.IsNullOrWhiteSpace(referralCode)) return;

        var referrerArtist = await db.Artists.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ReferralCode == referralCode);

        if (referrerArtist is null) return; // Código inválido — ignora silenciosamente

        // Evita duplicata (mesmo usuário indicado duas vezes)
        var alreadyExists = await db.Referrals.AnyAsync(
            r => r.ReferrerArtistId == referrerArtist.Id && r.ReferredUserId == referredUserId);
        if (alreadyExists) return;

        db.Referrals.Add(new Referral
        {
            ReferrerArtistId = referrerArtist.Id,
            ReferredUserId = referredUserId,
            ReferredUserName = referredUserName
        });

        await db.SaveChangesAsync();
    }
}
