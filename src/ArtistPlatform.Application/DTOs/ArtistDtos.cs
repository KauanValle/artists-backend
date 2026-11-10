using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Auth ───────────────────────────

public record UserDto(Guid Id, string Email, string DisplayName, UserRole Role, Guid? ArtistId, Guid? ContractorId, SubscriptionTier SubscriptionTier, bool PerfilCompleto, bool TutorialFeito, List<string> TutorialPassos, bool IsDeactivated = false, string? DeactivationMessage = null);

/// <summary>Progresso do tutorial de onboarding do usuário logado.</summary>
public record SalvarTutorialRequest(bool Feito, List<string> Passos);

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);

/// <summary>Cadastro unificado: só identidade; perfil (artista/contratante) é completado depois.</summary>
public record RegisterRequest(string Email, string Password, string Name);

public record GoogleLoginRequest(string IdToken);

/// <summary>Conclusão do perfil pós-registro: escolhe Artista ou Contratante e preenche os dados.</summary>
public record CompleteProfileRequest(
    string Tipo,
    string? ArtisticName,
    string? ArtistType,
    Guid? CategoryId,
    string? City,
    string? State,
    string? Phone,
    string? Cpf,
    string? Cep,
    List<string>? Styles,
    List<string>? EventTypes,
    string? CompanyName);

public record RegisterArtistRequest(
    string Email,
    string Password,
    string Name,
    string ArtisticName,
    string ArtistType,
    string City,
    string State,
    string Phone,
    Guid CategoryId,
    List<string>? Styles,
    List<string>? EventTypes);

public record RegisterContractorRequest(string Email, string Password, string Name, string ContractorType, string Phone, string? CompanyName);

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
    List<string>? EventTypes,
    List<string> GalleryUrls,
    List<string> VideoUrls,
    int TravelTimeMinutes = 0,
    string? Cpf = null,
    string? Cep = null,
    bool AceitaNegociarPropostas = true);

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
    List<string> EventTypes,
    List<string> GalleryUrls,
    List<string> VideoUrls,
    string Slug,
    bool IsPublished,
    double? AverageRating,
    int ReviewCount,
    decimal? StartingPrice,
    int TravelTimeMinutes,
    bool AceitaNegociarPropostas);

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
    List<string> EventTypes,
    List<string> GalleryUrls,
    List<string> VideoUrls,
    double? AverageRating,
    int ReviewCount,
    decimal? StartingPrice,
    bool AceitaNegociarPropostas,
    List<FeeDto> Fees,
    List<ReviewDto> Reviews);

// ─────────────────────────── Fees (cachês) ───────────────────────────

public record FeeDto(Guid Id, Guid ArtistId, int DurationMinutes, decimal Price, string? Description);

public record SaveFeeRequest(int DurationMinutes, decimal Price, string? Description);

// ─────────────────────────── Availability ───────────────────────────

public record AvailabilityDto(Guid Id, Guid ArtistId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, bool IsAllDay, AvailabilityStatus Status, string? Note);

public record SaveAvailabilityRequest(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, bool IsAllDay, AvailabilityStatus Status, string? Note);

/// <summary>Criação em lote: vários dias de uma vez (dias de trabalho padrão / datas da agenda visual).</summary>
public record SaveAvailabilityBatchRequest(
    List<DateOnly> Dates,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsAllDay,
    AvailabilityStatus Status,
    string? Note);

public record BatchAvailabilityResultDto(int Created, int SkippedExisting, int SkippedEventConflict);

/// <summary>Janela ocupada por evento (agendado/confirmado) — usada para bloquear horários na contratação.</summary>
public record BusySlotDto(DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, string Status);

// ─────────────────────────── Admin User Management ───────────────────────────

/// <summary>Estatísticas do artista para o painel admin.</summary>
public record AdminArtistStatsDto(
    Guid ArtistId,
    string Name,
    string ArtisticName,
    string? ProfilePhotoUrl,
    string CategoryName,
    string City,
    string State,
    double? AverageRating,
    int ReviewCount,
    bool IsPublished,
    bool IsDeactivated,
    string? DeactivationMessage,
    int TotalShows,
    int MonthShows,
    int YearShows,
    decimal ValorConfirmadosConcluidos,
    decimal TaxaAdministrativa);

/// <summary>Estatísticas do contratante para o painel admin.</summary>
public record AdminContractorStatsDto(
    Guid ContractorId,
    string Name,
    string? CompanyName,
    string? Phone,
    string City,
    string State,
    bool IsDeactivated,
    string? DeactivationMessage,
    int TotalContratados,
    int MonthContratados,
    int YearContratados,
    decimal TaxasPagas);

/// <summary>Requisição de desativação de conta pelo admin.</summary>
public record DeactivateUserRequest(string Message);

/// <summary>Listagem de artistas para o admin (todos os perfis).</summary>
public record AdminArtistListDto(
    Guid ArtistId,
    Guid UserId,
    string Name,
    string ArtisticName,
    string? ProfilePhotoUrl,
    string CategoryName,
    string City,
    string State,
    double? AverageRating,
    int ReviewCount,
    bool IsPublished,
    bool IsDeactivated,
    int TotalBookings);

/// <summary>Listagem de contratantes para o admin.</summary>
public record AdminContractorListDto(
    Guid ContractorId,
    Guid UserId,
    string Name,
    string? CompanyName,
    string City,
    string State,
    string? Phone,
    int TotalBookings,
    int ConfirmedBookings,
    decimal TotalSpent,
    bool IsDeactivated);
