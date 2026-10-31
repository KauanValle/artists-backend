using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Bookings ───────────────────────────

/// <summary>Adicional escolhido pelo contratante (equipe externa ou equipamento externo).</summary>
public record BookingAddonItemRequest(AddonItemType ItemType, Guid ItemId);

public record BookingAddonDto(
    Guid Id,
    AddonItemType ItemType,
    Guid ItemId,
    string Name,
    ShareType ShareType,
    decimal ShareValue,
    decimal Amount);

public record CreateBookingRequestDto(
    Guid ArtistId,
    DateOnly EventDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    EventType EventType,
    int? EstimatedAudience,
    decimal? Budget,
    string Message,
    List<BookingAddonItemRequest>? Addons);

/// <summary>Solicitação aberta pela rota pública (sem cadastro): dados de contato informados na hora.</summary>
public record CreateGuestBookingRequestDto(
    Guid ArtistId,
    string GuestName,
    string? GuestCompanyName,
    string GuestPhone,
    decimal Budget,
    DateOnly EventDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    EventType EventType,
    int? EstimatedAudience,
    string Message,
    List<BookingAddonItemRequest>? Addons);

public record BookingDto(
    Guid Id,
    Guid ArtistId,
    string ArtistName,
    string? ArtistPhotoUrl,
    Guid? ContractorId,
    string ContractorName,
    bool IsGuest,
    string? GuestCompanyName,
    string? GuestPhone,
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
    DateTime CreatedAt,
    List<BookingAddonDto> Addons);

// ─────────────────────────── Proposals ───────────────────────────

public record ProposalItemDto(Guid Id, string Description, decimal Amount);

public record ProposalDto(
    Guid Id,
    Guid BookingRequestId,
    Guid ArtistId,
    Guid? ContractorId,
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

// ─────────────────────────── Conclusão do show (dupla confirmação) ───────────────────────────

/// <summary>Estado da conclusão; ContractorToken vem preenchido enquanto falta o contratante (link público).</summary>
public record CompleteShowDto(
    Guid BookingId,
    BookingStatus Status,
    bool ArtistConfirmed,
    bool ContractorConfirmed,
    string? ContractorToken);

/// <summary>Resumo público do show para a página de confirmação do contratante (por token).</summary>
public record CompletionSummaryDto(
    string Token,
    string ArtistName,
    EventType EventType,
    DateOnly EventDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Location,
    bool ArtistConfirmed,
    bool ContractorConfirmed,
    BookingStatus Status);
