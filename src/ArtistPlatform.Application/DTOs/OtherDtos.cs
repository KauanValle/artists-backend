using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Events / Agenda ───────────────────────────

public record EventDto(
    Guid Id,
    Guid ArtistId,
    Guid? ContractorId,
    Guid? BookingRequestId,
    string Title,
    EventType Type,
    DateTime StartDateTime,
    DateTime EndDateTime,
    string Location,
    string Description,
    EventStatus Status);

public record CreateEventRequestDto(
    string Title,
    EventType Type,
    DateTime StartDateTime,
    DateTime EndDateTime,
    string Location,
    string Description);

public record UpdateEventRequestDto(
    string Title,
    EventType Type,
    DateTime StartDateTime,
    DateTime EndDateTime,
    string Location,
    string Description,
    EventStatus Status);

public record EventDetailDto(
    EventDto Event,
    List<EventEquipmentDto> Equipment,
    List<EventTeamShareDto> TeamShares,
    TeamDivisionDto? Division);

public record EventEquipmentDto(Guid Id, Guid EventId, Guid EquipmentId, string EquipmentName, bool IsChecked, string Notes);

public record AddEventEquipmentRequestDto(Guid EquipmentId, string? Notes);

public record EventTeamShareDto(Guid Id, Guid TeamMemberId, string MemberName, ShareType ShareType, decimal ShareValue);

public record SetEventTeamShareRequestDto(Guid TeamMemberId, ShareType ShareType, decimal ShareValue);

/// <summary>Divisão de cachê do evento: cachê − despesas = distribuível (PRD §13).</summary>
public record TeamDivisionDto(
    decimal EventFee,
    decimal Expenses,
    decimal Distributable,
    List<TeamDivisionLineDto> Lines);

public record TeamDivisionLineDto(Guid TeamMemberId, string MemberName, ShareType ShareType, decimal ShareValue, decimal Amount);

// ─────────────────────────── Financial ───────────────────────────

public record TransactionDto(
    Guid Id,
    TransactionType Type,
    string Category,
    decimal Amount,
    DateOnly? DueDate,
    DateOnly? SettlementDate,
    TransactionStatus Status,
    Guid? EventId,
    string? EventTitle,
    string Notes,
    DateTime CreatedAt);

public record CreateTransactionRequestDto(
    TransactionType Type,
    string Category,
    decimal Amount,
    DateOnly? DueDate,
    Guid? EventId,
    string Notes);

public record UpdateTransactionRequestDto(
    string Category,
    decimal Amount,
    DateOnly? DueDate,
    TransactionStatus Status,
    Guid? EventId,
    string Notes);

/// <summary>Dashboard financeiro (PRD §7/§12).</summary>
public record FinancialSummaryDto(
    DateTime? From,
    DateTime? To,
    decimal IncomeExpected,
    decimal IncomeReceived,
    decimal IncomePending,
    decimal ExpenseTotal,
    decimal ExpensePaid,
    decimal ExpensePending,
    decimal Result,
    decimal AccountsReceivable,
    int UpcomingShowsCount);

// ─────────────────────────── Team ───────────────────────────

public record TeamMemberDto(
    Guid Id,
    string Name,
    string PhotoUrl,
    string Role,
    string Phone,
    string Email,
    ShareType DefaultShareType,
    decimal DefaultShareValue,
    string Notes);

public record SaveTeamMemberRequestDto(
    string Name,
    string PhotoUrl,
    string Role,
    string Phone,
    string Email,
    ShareType DefaultShareType,
    decimal DefaultShareValue,
    string Notes);

// ─────────────────────────── Equipment ───────────────────────────

public record EquipmentDto(
    Guid Id,
    string Name,
    string Category,
    string Brand,
    string Model,
    string Identifier,
    decimal? WeightKg,
    decimal? Value,
    EquipmentStatus Status,
    string Notes);

public record SaveEquipmentRequestDto(
    string Name,
    string Category,
    string Brand,
    string Model,
    string Identifier,
    decimal? WeightKg,
    decimal? Value,
    EquipmentStatus Status,
    string Notes);

// ─────────────────────────── Reviews ───────────────────────────

public record ReviewDto(
    Guid Id,
    Guid ArtistId,
    Guid ContractorId,
    string ContractorName,
    Guid EventId,
    int OverallRating,
    int Punctuality,
    int Quality,
    int Professionalism,
    int Communication,
    string Comment,
    DateTime CreatedAt);

public record CreateReviewRequestDto(
    Guid EventId,
    int OverallRating,
    int Punctuality,
    int Quality,
    int Professionalism,
    int Communication,
    string Comment);

// ─────────────────────────── Favorites / Notifications / Contractor ───────────────────────────

public record FavoriteDto(Guid Id, Guid ArtistId, string ArtistName, string? ArtistPhotoUrl, string Slug, string CategoryName, string City);

public record NotificationDto(Guid Id, NotificationType Type, string Title, string Message, string? Link, bool IsRead, DateTime CreatedAt);

public record ContractorDto(
    Guid Id,
    ContractorType Type,
    string Name,
    string? CompanyName,
    string Phone,
    string City,
    string State);

public record SaveContractorRequestDto(ContractorType Type, string Name, string? CompanyName, string Phone, string City, string State);

public record UploadResultDto(string Url);
