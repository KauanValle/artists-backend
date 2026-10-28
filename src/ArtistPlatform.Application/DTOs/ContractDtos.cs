using ArtistPlatform.Domain.Enums;

namespace ArtistPlatform.Application.DTOs;

// ─────────────────────────── Contrato de show (Jurídico) ───────────────────────────

public record SignArtistContractRequest(
    string ArtistDocument,
    string ArtistAddress,
    string ArtistContact,
    string? ContractorDocument,
    string? ContractorRepresentative,
    string? ContractorAddress,
    string? ContractorContact,
    string PaymentTerms,
    string SignatureData);

public record ArtistContractDto(
    Guid BookingRequestId,
    bool Signed,
    string ContractText,
    string ContentHash,
    string ArtistName,
    string ArtisticName,
    string ArtistDocument,
    string ArtistAddress,
    string ArtistContact,
    string ContractorName,
    string ContractorDocument,
    string ContractorRepresentative,
    string ContractorAddress,
    string ContractorContact,
    string PaymentTerms,
    string EventDate,
    string StartTime,
    string EndTime,
    string Location,
    decimal? CacheValue,
    string? SignatureData,
    string? SignedName,
    DateTime? SignedAtUtc,
    string? SignedIp,
    string? SignedUserAgent);
