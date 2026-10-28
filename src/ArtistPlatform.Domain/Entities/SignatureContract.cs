using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Contrato de prestação de serviços artísticos de uma contratação, com assinatura eletrônica
/// do artista e evidências de formalidade (data/hora UTC com segundos, IP, user-agent e hash
/// SHA-256 do texto assinado). Um contrato por solicitação; reasinar substitui a assinatura anterior.
/// </summary>
public class SignatureContract : BaseEntity
{
    public Guid BookingRequestId { get; set; }
    public Guid ArtistId { get; set; }
    public Guid? ContractorId { get; set; }

    // Retrato integral do documento assinado (evidência do conteúdo).
    public string ContractText { get; set; } = default!;
    public string ContentHash { get; set; } = default!; // SHA-256 hex

    // Contratado (artista)
    public string ArtistName { get; set; } = default!;
    public string ArtistDocument { get; set; } = string.Empty;
    public string ArtistAddress { get; set; } = string.Empty;
    public string ArtistContact { get; set; } = string.Empty;

    // Contratante (dados informados pelo artista a partir da negociação)
    public string ContractorName { get; set; } = string.Empty;
    public string ContractorDocument { get; set; } = string.Empty;
    public string ContractorRepresentative { get; set; } = string.Empty;
    public string ContractorAddress { get; set; } = string.Empty;
    public string ContractorContact { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;

    // Assinatura eletrônica do artista
    public string? ArtistSignatureData { get; set; } // PNG em data URL (canvas)
    public string ArtistSignedName { get; set; } = string.Empty;
    public DateTime? ArtistSignedAtUtc { get; set; }
    public string? ArtistIp { get; set; }
    public string? ArtistUserAgent { get; set; }
}
