using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Auth ───────────────────────────

public record UserDto(Guid Id, string Email, string DisplayName, UserRole Role, Guid? ArtistId, Guid? ContractorId);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);

public record RegisterArtistRequest(string Email, string Password, string Name, string ArtisticName, string ArtistType, string City, string State, string Phone, Guid CategoryId);

public record RegisterContractorRequest(string Email, string Password, string Name, string ContractorType, string Phone);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

// ─────────────────────────── Artists ───────────────────────────

public record CategoryDto(Guid Id, string Name, string Slug);

public record SaveArtistRequest(
    string ArtistType,
    string Name,
    string ArtisticName,
    Guid CategoryId,
    string City,
    string State,
    string Phone,
    string? ProfilePhotoUrl,
    string Description,
    List<string> Styles,
    List<string> Specialties,
    List<string> GalleryUrls,
    List<string> VideoUrls);

public record ArtistDto(
    Guid Id,
    string ArtistType,
    string Name,
    string ArtisticName,
    Guid CategoryId,
    string CategoryName,
    string City,
    string State,
    string Phone,
    string? ProfilePhotoUrl,
    string Description,
    List<string> Styles,
    List<string> Specialties,
    List<string> GalleryUrls,
    List<string> VideoUrls,
    string Slug,
    bool IsPublished,
    double? AverageRating,
    int ReviewCount,
    decimal? StartingPrice);

/// <summary>Card do marketplace (PRD §17): foto, nome, avaliação, categoria, cidade, cachê inicial e disponibilidade.</summary>
public record ArtistCardDto(
    Guid Id,
    string Slug,
    string ArtisticName,
    string? ProfilePhotoUrl,
    Guid CategoryId,
    string CategoryName,
    string City,
    string State,
    double? AverageRating,
    int ReviewCount,
    decimal? StartingPrice,
    bool IsAvailableForRequestedSlot);

public record SearchArtistsQuery(
    Guid? CategoryId,
    string? City,
    DateOnly? Date,
    TimeOnly? StartTime,
    int? DurationMinutes,
    decimal? MinPrice,
    decimal? MaxPrice,
    double? MinRating,
    string? Search,
    int Page = 1,
    int PageSize = 12);

public record ArtistDetailPublicDto(
    Guid Id,
    string Slug,
    string ArtisticName,
    string ArtistType,
    string? ProfilePhotoUrl,
    Guid CategoryId,
    string CategoryName,
    string City,
    string State,
    string Description,
    List<string> Styles,
    List<string> Specialties,
    List<string> GalleryUrls,
    List<string> VideoUrls,
    double? AverageRating,
    int ReviewCount,
    decimal? StartingPrice,
    List<FeeDto> Fees,
    List<ReviewDto> Reviews);

// ─────────────────────────── Fees (cachês) ───────────────────────────

public record FeeDto(Guid Id, Guid ArtistId, int DurationMinutes, decimal Price, string? Description);

public record SaveFeeRequest(int DurationMinutes, decimal Price, string? Description);

// ─────────────────────────── Availability ───────────────────────────

public record AvailabilityDto(Guid Id, Guid ArtistId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, bool IsAllDay, AvailabilityStatus Status, string? Note);

public record SaveAvailabilityRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, bool IsAllDay, AvailabilityStatus Status, string? Note);
