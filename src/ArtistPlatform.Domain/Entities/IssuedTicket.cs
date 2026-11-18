using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Ingresso individual emitido quando o pedido é pago (um registro por unidade). O QR code do PDF contém
/// apenas o <see cref="Code"/> — um código aleatório, impossível de adivinhar — e a entrada é registrada aqui.
/// </summary>
public class IssuedTicket : BaseEntity
{
    public Guid TicketOrderId { get; set; }
    public Guid TicketEventId { get; set; }
    public Guid EventTicketId { get; set; }

    /// <summary>Número do ingresso dentro do pedido (1..Quantity).</summary>
    public int Sequence { get; set; }

    /// <summary>Código único e secreto (16 caracteres, sem ambíguos), usado no QR code.</summary>
    public string Code { get; set; } = default!;

    public DateTime? CheckedInAt { get; set; }

    /// <summary>Quem registrou a entrada: "Produtor" ou o nome da portaria.</summary>
    public string? CheckedInBy { get; set; }
    public Guid? CheckedInGateId { get; set; }
}
