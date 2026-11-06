using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IReviewService
{
    Task<ReviewDto> CreateAsync(CreateReviewRequestDto request);
    Task<List<ReviewDto>> ListForArtistAsync(Guid artistId);
    Task<List<PendingReviewDto>> PendingForContractorAsync(Guid contractorId);
    Task<List<ReviewDto>> ListReceivedAsync(Guid? artistUserId);
}

/// <summary>Avaliações (PRD §21): RN014, RN015 e RN016.</summary>
public class ReviewService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeClock clock,
    INotificationService notifications) : IReviewService
{
    private const int MaxPhotos = 6;

    public async Task<ReviewDto> CreateAsync(CreateReviewRequestDto request)
    {
        var contractorId = currentUser.EnsureContractor();

        if (!Review.IsValidRating(request.OverallRating))
            throw new ValidationException("A nota deve estar entre 0 e 5 estrelas.");

        var ev = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.EventId)
            ?? throw new NotFoundException("Evento não encontrado.");

        // RN014: somente o contratante participante do evento pode avaliar
        if (ev.ContractorId != contractorId)
            throw new ForbiddenException("RN014: apenas o contratante participante do evento pode avaliar.");

        // RN016: artista não avalia a si próprio (avaliação é do contratante ao artista)
        var artistUserId = await db.Artists.AsNoTracking()
            .Where(a => a.Id == ev.ArtistId).Select(a => a.UserId).FirstAsync();
        if (currentUser.UserId == artistUserId)
            throw new ForbiddenException("RN016: o artista não pode avaliar o próprio evento.");

        // RN015: somente após a conclusão do show por ambas as partes (ou após a data, para eventos antigos)
        if (ev.Status != EventStatus.Completed && DateOnly.FromDateTime(ev.StartDateTime) > clock.Today)
            throw new BusinessRuleException("RN015: a avaliação só é liberada após a conclusão do show.");

        var alreadyReviewed = await db.Reviews.AnyAsync(r => r.EventId == request.EventId && r.ContractorId == contractorId);
        if (alreadyReviewed)
            throw new BusinessRuleException("Este evento já foi avaliado.");

        var photos = (request.Photos ?? new List<string>())
            .Select(u => u.Trim())
            .Where(u => u.Length > 0)
            .Distinct()
            .Take(MaxPhotos)
            .ToList();

        var review = new Review
        {
            ArtistId = ev.ArtistId,
            ContractorId = contractorId,
            EventId = request.EventId,
            OverallRating = request.OverallRating,
            // O novo fluxo usa uma única nota em estrelas; as dimensões herdadas recebem a mesma nota.
            Punctuality = request.OverallRating,
            Quality = request.OverallRating,
            Professionalism = request.OverallRating,
            Communication = request.OverallRating,
            Comment = request.Comment ?? string.Empty
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        for (var i = 0; i < photos.Count; i++)
            db.ReviewPhotos.Add(new ReviewPhoto { ReviewId = review.Id, Url = photos[i], Order = i });
        if (photos.Count > 0)
            await db.SaveChangesAsync();

        await notifications.NotifyAsync(artistUserId, NotificationType.NewReview,
            "Nova avaliação recebida",
            $"Você recebeu uma avaliação de {request.OverallRating}/5 estrelas.",
            "/avaliacoes");

        return await ToDtoAsync(review, contractorName: await GetContractorNameAsync(contractorId));
    }

    public async Task<List<ReviewDto>> ListForArtistAsync(Guid artistId)
    {
        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.ArtistId == artistId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        var contractorIds = reviews.Select(r => r.ContractorId).Distinct().ToList();
        var names = await db.Contractors.AsNoTracking()
            .Where(c => contractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var result = new List<ReviewDto>();
        foreach (var r in reviews)
            result.Add(await ToDtoAsync(r, names.GetValueOrDefault(r.ContractorId, "Contratante")));
        return result;
    }

    /// <summary>Shows concluídos (por ambas as partes) que o contratante ainda não avaliou.</summary>
    public async Task<List<PendingReviewDto>> PendingForContractorAsync(Guid contractorId)
    {
        var pendings = await db.Events.AsNoTracking()
            .Where(e => e.ContractorId == contractorId
                && e.Status == EventStatus.Completed
                && !db.Reviews.Any(r => r.EventId == e.Id && r.ContractorId == contractorId))
            .OrderByDescending(e => e.EndDateTime)
            .Select(e => new
            {
                e.Id,
                e.ArtistId,
                e.Title,
                e.StartDateTime,
                e.Location
            })
            .ToListAsync();

        if (pendings.Count == 0) return new List<PendingReviewDto>();

        var artistIds = pendings.Select(p => p.ArtistId).Distinct().ToList();
        var artists = await db.Artists.AsNoTracking()
            .Where(a => artistIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.ArtisticName, a.ProfilePhotoUrl });

        return pendings.Select(p =>
        {
            var artist = artists.GetValueOrDefault(p.ArtistId);
            return new PendingReviewDto(
                p.Id, p.ArtistId, artist != null ? artist.ArtisticName : "Artista",
                artist != null ? artist.ProfilePhotoUrl : null,
                p.Title, p.StartDateTime, p.Location);
        }).ToList();
    }

    /// <summary>Avaliações recebidas pelo artista logado (somente leitura).</summary>
    public async Task<List<ReviewDto>> ListReceivedAsync(Guid? artistUserId)
    {
        var artistId = await db.Artists.AsNoTracking()
            .Where(a => a.UserId == artistUserId)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Perfil de artista não encontrado.");
        return await ListForArtistAsync(artistId);
    }

    private async Task<ReviewDto> ToDtoAsync(Review r, string contractorName)
    {
        var photos = await db.ReviewPhotos.AsNoTracking()
            .Where(p => p.ReviewId == r.Id)
            .OrderBy(p => p.Order)
            .Select(p => p.Url)
            .ToListAsync();
        return new ReviewDto(r.Id, r.ArtistId, r.ContractorId, contractorName, r.EventId,
            r.OverallRating, r.Punctuality, r.Quality, r.Professionalism, r.Communication,
            r.Comment, r.CreatedAt, photos);
    }

    private async Task<string> GetContractorNameAsync(Guid contractorId)
    {
        return await db.Contractors.AsNoTracking()
            .Where(c => c.Id == contractorId).Select(c => c.Name).FirstOrDefaultAsync() ?? "Contratante";
    }
}
