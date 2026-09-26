using System.Text;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Common;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IArtistService
{
    Task<ArtistDto> GetMyArtistAsync(Guid userId);
    Task<ArtistDetailPublicDto> GetPublicAsync(Guid artistId);
    Task<ArtistDetailPublicDto> GetBySlugAsync(string slug);
    Task<ArtistDto> SaveOnboardingAsync(Guid userId, SaveArtistRequest request);
    Task PublishAsync(Guid userId, bool publish);
    Task<PagedResult<ArtistCardDto>> SearchAsync(SearchArtistsQuery query);
    Task<List<CategoryDto>> GetCategoriesAsync();
}

public class ArtistService(IAppDbContext db) : IArtistService
{
    public async Task<ArtistDto> GetMyArtistAsync(Guid userId)
    {
        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new NotFoundException("Perfil de artista não encontrado.");
        return await BuildArtistDtoAsync(artist);
    }

    public async Task<ArtistDetailPublicDto> GetPublicAsync(Guid artistId)
    {
        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == artistId)
            ?? throw new NotFoundException("Artista não encontrado.");
        return await BuildPublicDtoAsync(artist);
    }

    public async Task<ArtistDetailPublicDto> GetBySlugAsync(string slug)
    {
        var profile = await db.ArtistProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug)
            ?? throw new NotFoundException("Artista não encontrado.");
        var artist = await db.Artists.AsNoTracking().FirstAsync(a => a.Id == profile.ArtistId);
        return await BuildPublicDtoAsync(artist);
    }

    /// <summary>Onboarding do artista (PRD §6): cria/atualiza artista + perfil público.</summary>
    public async Task<ArtistDto> SaveOnboardingAsync(Guid userId, SaveArtistRequest request)
    {
        if (!Enum.TryParse<ArtistType>(request.ArtistType, ignoreCase: true, out var artistType))
            throw new ValidationException("Tipo de artista inválido. Use Solo ou Band.");

        var category = await db.ArtistCategories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive)
            ?? throw new ValidationException("Categoria inválida.");

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ArtisticName))
            throw new ValidationException("Nome e nome artístico são obrigatórios.");

        var artist = await db.Artists.FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new NotFoundException("Perfil de artista não encontrado.");

        artist.Type = artistType;
        artist.Name = request.Name.Trim();
        artist.ArtisticName = request.ArtisticName.Trim();
        artist.CategoryId = request.CategoryId;
        artist.City = request.City.Trim();
        artist.State = request.State.Trim();
        artist.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.ProfilePhotoUrl))
            artist.ProfilePhotoUrl = request.ProfilePhotoUrl;
        artist.UpdatedAt = DateTime.UtcNow;

        var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artist.Id);
        if (profile is null)
        {
            profile = new ArtistProfile
            {
                ArtistId = artist.Id,
                Slug = await GenerateUniqueSlugAsync(request.ArtisticName)
            };
            db.ArtistProfiles.Add(profile);
        }

        profile.Description = request.Description ?? string.Empty;
        profile.Styles = request.Styles ?? [];
        profile.Specialties = request.Specialties ?? [];
        profile.GalleryUrls = request.GalleryUrls ?? [];
        profile.VideoUrls = request.VideoUrls ?? [];
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return await BuildArtistDtoAsync(artist);
    }

    /// <summary>Publica o perfil público; exige dados mínimos preenchidos.</summary>
    public async Task PublishAsync(Guid userId, bool publish)
    {
        var artist = await db.Artists.FirstOrDefaultAsync(a => a.UserId == userId)
            ?? throw new NotFoundException("Perfil de artista não encontrado.");
        var profile = await db.ArtistProfiles.FirstOrDefaultAsync(p => p.ArtistId == artist.Id)
            ?? throw new NotFoundException("Complete o onboarding antes de publicar.");

        if (publish)
        {
            var incomplete = string.IsNullOrWhiteSpace(artist.Name)
                || string.IsNullOrWhiteSpace(artist.ArtisticName)
                || string.IsNullOrWhiteSpace(artist.City)
                || artist.CategoryId == Guid.Empty
                || string.IsNullOrWhiteSpace(profile.Description);
            if (incomplete)
                throw new BusinessRuleException("Complete nome, categoria, cidade e descrição antes de publicar o perfil.");
        }

        profile.IsPublished = publish;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Busca do marketplace (PRD §17). RN020: quando data + horário + duração são informados,
    /// a disponibilidade é validada para o intervalo exato (nem apenas para a data).
    /// </summary>
    public async Task<PagedResult<ArtistCardDto>> SearchAsync(SearchArtistsQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 50);

        var artists = await db.Artists.AsNoTracking()
            .Include(a => a.Category)
            .Where(a => db.ArtistProfiles.Any(p => p.ArtistId == a.Id && p.IsPublished))
            .ToListAsync();

        var profiles = await db.ArtistProfiles.AsNoTracking().ToListAsync();
        var profileByArtist = profiles.GroupBy(p => p.ArtistId).ToDictionary(g => g.Key, g => g.First());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            artists = artists.Where(a =>
                    a.ArtisticName.ToLowerInvariant().Contains(term) ||
                    a.Name.ToLowerInvariant().Contains(term))
                .ToList();
        }

        if (query.CategoryId.HasValue)
            artists = artists.Where(a => a.CategoryId == query.CategoryId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLowerInvariant();
            artists = artists.Where(a => a.City.ToLowerInvariant().Contains(city)).ToList();
        }

        // Cachê inicial ("a partir de R$ X") por artista
        var minPriceByArtist = await db.ArtistFees.AsNoTracking()
            .GroupBy(f => f.ArtistId)
            .Select(g => new { ArtistId = g.Key, Min = g.Min(f => f.Price) })
            .ToDictionaryAsync(g => g.ArtistId, g => g.Min);

        if (query.MinPrice.HasValue)
            artists = artists.Where(a =>
                minPriceByArtist.ContainsKey(a.Id) && minPriceByArtist[a.Id] >= query.MinPrice.Value).ToList();
        if (query.MaxPrice.HasValue)
            artists = artists.Where(a =>
                minPriceByArtist.ContainsKey(a.Id) && minPriceByArtist[a.Id] <= query.MaxPrice.Value).ToList();

        // Avaliações agregadas
        var ratings = await db.Reviews.AsNoTracking()
            .GroupBy(r => r.ArtistId)
            .Select(g => new { ArtistId = g.Key, Avg = g.Average(r => (double)r.OverallRating), Count = g.Count() })
            .ToDictionaryAsync(g => g.ArtistId, g => (g.Avg, g.Count));

        if (query.MinRating.HasValue)
            artists = artists.Where(a =>
                ratings.ContainsKey(a.Id) && ratings[a.Id].Avg >= query.MinRating.Value).ToList();

        // RN020: bloqueios no intervalo exato solicitado
        Interval? requested = null;
        if (query.Date.HasValue && query.StartTime.HasValue && query.DurationMinutes is > 0)
        {
            var start = query.Date.Value.ToDateTime(query.StartTime.Value);
            requested = new Interval(start, start.AddMinutes(query.DurationMinutes.Value));
        }

        var blockedArtistIds = new HashSet<Guid>();
        if (requested is not null)
        {
            var requestedDay = query.Date!.Value;

            var blockedRows = await db.ArtistAvailabilities.AsNoTracking()
                .Where(a => a.Status != AvailabilityStatus.Available && a.Date == requestedDay)
                .ToListAsync();
            foreach (var row in blockedRows)
            {
                var interval = new Interval(
                    row.Date.ToDateTime(row.StartTime),
                    row.Date.ToDateTime(row.IsAllDay ? new TimeOnly(23, 59, 59) : row.EndTime));
                if (interval.Overlaps(requested))
                    blockedArtistIds.Add(row.ArtistId);
            }

            var busy = await db.Events.AsNoTracking()
                .Where(e => (e.Status == EventStatus.Scheduled || e.Status == EventStatus.Confirmed) &&
                            e.StartDateTime < requested.End &&
                            e.EndDateTime > requested.Start)
                .Select(e => e.ArtistId)
                .Distinct()
                .ToListAsync();
            foreach (var artistId in busy)
                blockedArtistIds.Add(artistId);
        }

        var cards = artists.Select(a =>
        {
            var rating = ratings.GetValueOrDefault(a.Id);
            var profile = profileByArtist.GetValueOrDefault(a.Id);
            return new ArtistCardDto(
                a.Id,
                profile?.Slug ?? string.Empty,
                a.ArtisticName,
                string.IsNullOrWhiteSpace(a.ProfilePhotoUrl) ? null : a.ProfilePhotoUrl,
                a.CategoryId,
                a.Category.Name,
                a.City,
                a.State,
                rating.Count > 0 ? Math.Round(rating.Avg, 1) : null,
                rating.Count,
                minPriceByArtist.GetValueOrDefault(a.Id),
                !blockedArtistIds.Contains(a.Id));
        }).ToList();

        var total = cards.Count;
        var items = cards
            .OrderBy(c => c.ArtisticName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedResult<ArtistCardDto>(items, page, pageSize, total);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await db.ArtistCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync();
    }

    private async Task<ArtistDto> BuildArtistDtoAsync(Artist artist)
    {
        var profile = await db.ArtistProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.ArtistId == artist.Id);
        var category = await db.ArtistCategories.AsNoTracking().FirstAsync(c => c.Id == artist.CategoryId);
        var (avg, count) = await GetRatingAsync(artist.Id);
        var startingPrice = await db.ArtistFees.AsNoTracking()
            .Where(f => f.ArtistId == artist.Id)
            .MinAsync(f => (decimal?)f.Price);

        return new ArtistDto(
            artist.Id, artist.Type.ToString(), artist.Name, artist.ArtisticName,
            artist.CategoryId, category.Name, artist.City, artist.State, artist.Phone,
            artist.ProfilePhotoUrl, profile?.Description ?? string.Empty,
            profile?.Styles ?? [], profile?.Specialties ?? [], profile?.GalleryUrls ?? [], profile?.VideoUrls ?? [],
            profile?.Slug ?? string.Empty, profile?.IsPublished ?? false,
            avg, count, startingPrice);
    }

    private async Task<ArtistDetailPublicDto> BuildPublicDtoAsync(Artist artist)
    {
        var profile = await db.ArtistProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.ArtistId == artist.Id)
            ?? throw new NotFoundException("Perfil público não encontrado.");
        var category = await db.ArtistCategories.AsNoTracking().FirstAsync(c => c.Id == artist.CategoryId);

        var fees = await db.ArtistFees.AsNoTracking()
            .Where(f => f.ArtistId == artist.Id)
            .OrderBy(f => f.DurationMinutes)
            .Select(f => new FeeDto(f.Id, f.ArtistId, f.DurationMinutes, f.Price, f.Description))
            .ToListAsync();

        var reviews = await db.Reviews.AsNoTracking()
            .Where(r => r.ArtistId == artist.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();
        var contractorIds = reviews.Select(r => r.ContractorId).Distinct().ToList();
        var contractors = await db.Contractors.AsNoTracking()
            .Where(c => contractorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var (avg, count) = await GetRatingAsync(artist.Id);

        return new ArtistDetailPublicDto(
            artist.Id, profile.Slug, artist.ArtisticName, artist.Type.ToString(),
            string.IsNullOrWhiteSpace(artist.ProfilePhotoUrl) ? null : artist.ProfilePhotoUrl,
            artist.CategoryId, category.Name, artist.City, artist.State,
            profile.Description, profile.Styles, profile.Specialties, profile.GalleryUrls, profile.VideoUrls,
            avg, count, fees.Count > 0 ? fees.Min(f => f.Price) : null,
            fees,
            reviews.Select(r => new ReviewDto(
                r.Id, r.ArtistId, r.ContractorId, contractors.GetValueOrDefault(r.ContractorId, "Contratante"),
                r.EventId, r.OverallRating, r.Punctuality, r.Quality, r.Professionalism, r.Communication,
                r.Comment, r.CreatedAt)).ToList());
    }

    private async Task<(double? Avg, int Count)> GetRatingAsync(Guid artistId)
    {
        var ratings = await db.Reviews.AsNoTracking()
            .Where(r => r.ArtistId == artistId)
            .Select(r => (double)r.OverallRating)
            .ToListAsync();
        return ratings.Count == 0 ? (null, 0) : (Math.Round(ratings.Average(), 1), ratings.Count);
    }

    private async Task<string> GenerateUniqueSlugAsync(string artisticName)
    {
        var baseSlug = Slugify(artisticName);
        var slug = baseSlug;
        var exists = await db.ArtistProfiles.AnyAsync(p => p.Slug == slug);
        while (exists)
        {
            slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";
            exists = await db.ArtistProfiles.AnyAsync(p => p.Slug == slug);
        }
        return slug;
    }

    public static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch)) sb.Append('-');
            else if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        }
        var slug = sb.ToString().Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? "artista" : slug;
    }
}
