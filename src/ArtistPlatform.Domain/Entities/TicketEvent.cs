using ArtistPlatform.Domain.Common;

namespace ArtistPlatform.Domain.Entities;

/// <summary>
/// Evento do emissor de ingressos. Pertence ao usuário que o criou (artista ou contratante —
/// os dois acessam o mesmo painel). Não confundir com <see cref="Event"/>, que é o show
/// contratado de um artista.
/// </summary>
public class TicketEvent : BaseEntity
{
    /// <summary>Usuário (AppUser) dono do evento.</summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>URL da imagem de divulgação (retornada por /api/uploads).</summary>
    public string ImageUrl { get; set; } = default!;

    /// <summary>Festa, Festival, Show, Clipe musical etc.</summary>
    public string Category { get; set; } = default!;

    // Horários "wall clock" (sem fuso), como no restante do sistema.
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly EndTime { get; set; }

    public string Description { get; set; } = string.Empty;

    // Local
    public string VenueName { get; set; } = default!;
    public string Cep { get; set; } = default!;
    public string Street { get; set; } = default!;
    public string Number { get; set; } = default!;
    public string Complement { get; set; } = string.Empty;
    public string Neighborhood { get; set; } = default!;
    public string City { get; set; } = default!;
    /// <summary>UF com 2 letras.</summary>
    public string State { get; set; } = default!;

    // Produtor
    public string ProducerName { get; set; } = default!;
    public string ProducerDescription { get; set; } = string.Empty;

    /// <summary>Ingressos à venda deste evento.</summary>
    public List<EventTicket> Tickets { get; set; } = [];
}
