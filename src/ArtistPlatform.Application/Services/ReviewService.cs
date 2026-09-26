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
}

/// <summary>Avaliações (PRD §21): RN014, RN015 e RN016.</summary>
public class ReviewService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeClock clock,
    INotificationService notifications) : IReviewService
{
    public async Task<ReviewDto> CreateAsync(CreateReviewRequestDto request)
    {
        var contractorId = currentUser.EnsureContractor();

        if (!Review.IsValidRating(request.OverallRating) ||
            !Review.IsValidRating(request.Punctuality) ||
            !Review.IsValidRating(request.Quality) ||
            !Review.IsValidRating(request.Professionalism) ||
            !Review.IsValidRating(request.Communication))
            throw new ValidationException("As notas devem estar entre 1 e 5.");

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

        // RN015: somente após a data do evento
        if (ev.Status != EventStatus.Completed && DateOnly.FromDateTime(ev.StartDateTime) > clock.Today)
            throw new BusinessRuleException("RN015: a avaliação só é liberada após a data do evento.");

        var alreadyReviewed = await db.Reviews.AnyAsync(r => r.EventId == request.EventId && r.ContractorId == contractorId);
        if (alreadyReviewed)
            throw new BusinessRuleException("Este evento já foi avaliado.");

        var review = new Review
        {
            ArtistId = ev.ArtistId,
            ContractorId = contractorId,
            EventId = request.EventId,
            OverallRating = request.OverallRating,
            Punctuality = request.Punctuality,
            Quality = request.Quality,
            Professionalism = request.Professionalism,
            Communication = request.Communication,
            Comment = request.Comment ?? string.Empty
        };
        db.Reviews.Add(review);
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(artistUserId, NotificationType.NewReview,
            "Nova avaliação recebida",
            $"Você recebeu uma avaliação de {request.OverallRating}/5 estrelas.",
            "/perfil");

        return new ReviewDto(review.Id, review.ArtistId, review.ContractorId,
            await GetContractorNameAsync(contractorId), review.EventId,
            review.OverallRating, review.Punctuality, review.Quality, review.Professionalism,
            review.Communication, review.Comment, review.CreatedAt);
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

        return reviews.Select(r => new ReviewDto(
            r.Id, r.ArtistId, r.ContractorId, names.GetValueOrDefault(r.ContractorId, "Contratante"),
            r.EventId, r.OverallRating, r.Punctuality, r.Quality, r.Professionalism, r.Communication,
            r.Comment, r.CreatedAt)).ToList();
    }

    private async Task<string> GetContractorNameAsync(Guid contractorId)
    {
        return await db.Contractors.AsNoTracking()
            .Where(c => c.Id == contractorId).Select(c => c.Name).FirstOrDefaultAsync() ?? "Contratante";
    }
}
