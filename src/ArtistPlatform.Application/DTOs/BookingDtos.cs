using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Bookings ───────────────────────────

public record CreateBookingRequestDto(
    Guid ArtistId,
    DateOnly EventDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    EventType EventType,
    int? EstimatedAudience,
    decimal? Budget,
    string Message);

public record BookingDto(
    Guid Id,
    Guid ArtistId,
    string ArtistName,
    string? ArtistPhotoUrl,
    Guid ContractorId,
    string ContractorName,
    DateOnly EventDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    EventType EventType,
    int? EstimatedAudience,
    decimal? Budget,
    string Message,
    BookingStatus Status,
    Guid? EventId,
    Guid? AcceptedProposalId,
    Guid ConversationId,
    DateTime CreatedAt);

// ─────────────────────────── Proposals ───────────────────────────

public record ProposalItemDto(Guid Id, string Description, decimal Amount);

public record ProposalDto(
    Guid Id,
    Guid BookingRequestId,
    Guid ArtistId,
    Guid ContractorId,
    ProposalStatus Status,
    List<ProposalItemDto> Items,
    decimal TravelCost,
    decimal EquipmentCost,
    decimal Discount,
    decimal FinalAmount,
    DateTime ValidUntil,
    string Notes,
    DateTime? RespondedAt,
    DateTime CreatedAt);

public record CreateProposalRequestDto(
    Guid BookingRequestId,
    List<CreateProposalItemDto> Items,
    decimal TravelCost,
    decimal EquipmentCost,
    decimal Discount,
    int ValidityDays,
    string Notes);

public record CreateProposalItemDto(string Description, decimal Amount);

// ─────────────────────────── Conversations / Chat ───────────────────────────

public record ConversationDto(
    Guid Id,
    Guid BookingRequestId,
    Guid OtherUserId,
    string OtherUserName,
    string? OtherUserPhotoUrl,
    string? LastMessage,
    DateTime? LastMessageAt,
    int UnreadCount);

public record MessageDto(Guid Id, Guid SenderUserId, string SenderName, string Content, DateTime SentAt, bool IsRead);

public record SendMessageRequestDto(string Content);
