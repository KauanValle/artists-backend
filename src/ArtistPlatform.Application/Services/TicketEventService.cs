using System.Globalization;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

// ─────────────────────────── DTOs ───────────────────────────

/// <summary>Datas no formato yyyy-MM-dd e horários em HH:mm (mesmo formato dos inputs do frontend).</summary>
public record TicketEventDto(
    Guid Id,
    string Name,
    string ImageUrl,
    string Category,
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string Description,
    string Venue,
    string Cep,
    string Street,
    string Number,
    string Complement,
    string Neighborhood,
    string City,
    string State,
    string ProducerName,
    string ProducerDescription,
    DateTime CreatedAt,
    List<EventTicketDto> Tickets);

public record EventTicketDto(
    Guid Id,
    string Title,
    int Quantity,
    decimal ReceiveAmount,
    decimal FeePercent,
    decimal BuyerPrice,
    string SalesStartDate,
    string SalesEndDate,
    string Description,
    /// <summary>Ingressos vendidos (pagos).</summary>
    int Sold,
    /// <summary>Ingressos reservados por pedidos aguardando pagamento.</summary>
    int Reserved,
    /// <summary>Ainda à venda: Quantity - Sold - Reserved.</summary>
    int Available);

public record SaveTicketEventRequestDto(
    string Name,
    string ImageUrl,
    string Category,
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string Description,
    string Venue,
    string Cep,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string ProducerName,
    string ProducerDescription,
    List<SaveEventTicketRequestDto>? Tickets);

/// <summary>O valor pago pelo comprador NÃO vem do cliente: é calculado aqui com a taxa vigente.</summary>
public record SaveEventTicketRequestDto(
    string Title,
    int Quantity,
    decimal ReceiveAmount,
    string SalesStartDate,
    string SalesEndDate,
    string? Description);

public record TicketFeeDto(decimal FeePercent);

// ─────────────────────────── Interface ───────────────────────────

public interface ITicketEventService
{
    /// <summary>Eventos do usuário logado (mais recentes primeiro).</summary>
    Task<List<TicketEventDto>> ListAsync();
    Task<TicketEventDto> GetAsync(Guid id);

    /// <summary>Página pública do evento (sem login): qualquer pessoa com o link pode ver.</summary>
    Task<TicketEventDto> GetPublicAsync(Guid id);
    /// <summary>Lista todos os eventos da plataforma (públicos, mais recentes primeiro).</summary>
    Task<List<TicketEventDto>> ListAllPublicAsync();
    Task<TicketEventDto> CreateAsync(SaveTicketEventRequestDto request);
    Task<TicketEventDto> UpdateAsync(Guid id, SaveTicketEventRequestDto request);
    Task DeleteAsync(Guid id);

    /// <summary>Percentual da taxa administrativa aplicada sobre o valor a receber dos ingressos.</summary>
    Task<TicketFeeDto> GetFeeAsync();
}

// ─────────────────────────── Implementação ───────────────────────────

public class TicketEventService(IAppDbContext db, ICurrentUserService currentUser) : ITicketEventService
{
    /// <summary>Chave em AppSettings — o painel admin passará a editar este valor.</summary>
    public const string FeeSettingsKey = "ingressos:taxa";

    /// <summary>Taxa usada enquanto o admin não configurar (mesmo padrão da taxa administrativa dos shows).</summary>
    public const decimal DefaultFeePercent = 5m;

    private const decimal MaxFeePercent = 50m;
    private const int MaxTicketsPerEvent = 20;
    private const int MaxTicketQuantity = 1_000_000;
    private const decimal MaxReceiveAmount = 100_000m;

    private const string DateFormat = "yyyy-MM-dd";
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss"];

    public async Task<List<TicketEventDto>> ListAsync()
    {
        var userId = currentUser.EnsureAuthenticated();
        var events = await db.TicketEvents.AsNoTracking()
            .Include(e => e.Tickets)
            .Where(e => e.OwnerUserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        var pending = await PendingReservationsAsync(events);
        return events.Select(e => ToDto(e, pending)).ToList();
    }

    public async Task<TicketEventDto> GetAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var ev = await db.TicketEvents.AsNoTracking()
            .Include(e => e.Tickets)
            .FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");
        return ToDto(ev, await PendingReservationsAsync([ev]));
    }

    public async Task<TicketEventDto> GetPublicAsync(Guid id)
    {
        var ev = await db.TicketEvents.AsNoTracking()
            .Include(e => e.Tickets)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException("Evento não encontrado.");
        return ToDto(ev, await PendingReservationsAsync([ev]));
    }

    public async Task<List<TicketEventDto>> ListAllPublicAsync()
    {
        var events = await db.TicketEvents.AsNoTracking()
            .Include(e => e.Tickets)
            .OrderByDescending(e => e.StartDate)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync();
        var pending = await PendingReservationsAsync(events);
        return events.Select(e => ToDto(e, pending)).ToList();
    }

    public async Task<TicketFeeDto> GetFeeAsync()
    {
        currentUser.EnsureAuthenticated();
        return new TicketFeeDto(await GetFeePercentAsync());
    }

    public async Task<TicketEventDto> CreateAsync(SaveTicketEventRequestDto request)
    {
        var userId = currentUser.EnsureAuthenticated();

        var name = Required(request.Name, "o nome do evento", 3, 200);
        var imageUrl = Required(request.ImageUrl, "a imagem de divulgação", 1, 500);
        var category = Required(request.Category, "a categoria", 1, 60);
        var description = Required(request.Description, "a descrição do evento", 10, 2000);
        var venue = Required(request.Venue, "o nome do local", 2, 200);
        var street = Required(request.Street, "a rua", 2, 200);
        var number = Required(request.Number, "o número", 1, 20);
        var neighborhood = Required(request.Neighborhood, "o bairro", 2, 120);
        var city = Required(request.City, "a cidade", 2, 120);
        var producerName = Required(request.ProducerName, "o nome do produtor", 2, 200);
        var producerDescription = Required(request.ProducerDescription, "a descrição do produtor", 10, 1000);

        var complement = (request.Complement ?? string.Empty).Trim();
        if (complement.Length > 100)
            throw new ValidationException("O complemento deve ter no máximo 100 caracteres.");

        var state = (request.State ?? string.Empty).Trim().ToUpperInvariant();
        if (state.Length != 2 || !state.All(char.IsAsciiLetter))
            throw new ValidationException("Informe o estado com a sigla de 2 letras (UF).");

        var cepDigits = new string((request.Cep ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cepDigits.Length != 8)
            throw new ValidationException("CEP inválido.");
        var cep = $"{cepDigits[..5]}-{cepDigits[5..]}";

        var startDate = ParseDate(request.StartDate, "de início");
        var startTime = ParseTime(request.StartTime, "de início");
        var endDate = ParseDate(request.EndDate, "de término");
        var endTime = ParseTime(request.EndTime, "de término");

        // O sistema trata os horários como hora do Brasil (UTC-3).
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        if (startDate < today)
            throw new ValidationException("A data de início não pode estar no passado.");

        var start = startDate.ToDateTime(startTime);
        var end = endDate.ToDateTime(endTime);
        if (end <= start)
            throw new ValidationException("O término do evento deve ser depois do início.");

        var ev = new TicketEvent
        {
            OwnerUserId = userId,
            Name = name,
            ImageUrl = imageUrl,
            Category = category,
            StartDate = startDate,
            StartTime = startTime,
            EndDate = endDate,
            EndTime = endTime,
            Description = description,
            VenueName = venue,
            Cep = cep,
            Street = street,
            Number = number,
            Complement = complement,
            Neighborhood = neighborhood,
            City = city,
            State = state,
            ProducerName = producerName,
            ProducerDescription = producerDescription
        };

        var tickets = request.Tickets ?? [];
        if (tickets.Count > MaxTicketsPerEvent)
            throw new ValidationException($"Um evento pode ter no máximo {MaxTicketsPerEvent} tipos de ingresso.");

        if (tickets.Count > 0)
        {
            var feePercent = await GetFeePercentAsync();
            foreach (var t in tickets)
                ev.Tickets.Add(BuildTicket(t, feePercent, today, endDate));
        }

        db.TicketEvents.Add(ev);
        await db.SaveChangesAsync();
        return ToDto(ev);
    }


    public async Task<TicketEventDto> UpdateAsync(Guid id, SaveTicketEventRequestDto request)
    {
        var userId = currentUser.EnsureAuthenticated();
        var ev = await db.TicketEvents
            .Include(e => e.Tickets)
            .FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");

        // Valida campos
        ev.Name = Required(request.Name, "o nome do evento", 3, 200);
        ev.ImageUrl = Required(request.ImageUrl, "a imagem de divulgação", 1, 500);
        ev.Category = Required(request.Category, "a categoria", 1, 60);
        ev.Description = Required(request.Description, "a descrição do evento", 10, 2000);
        ev.VenueName = Required(request.Venue, "o nome do local", 2, 200);
        ev.Street = Required(request.Street, "a rua", 2, 200);
        ev.Number = Required(request.Number, "o número", 1, 20);
        ev.Neighborhood = Required(request.Neighborhood, "o bairro", 2, 120);
        ev.City = Required(request.City, "a cidade", 2, 120);
        ev.ProducerName = Required(request.ProducerName, "o nome do produtor", 2, 200);
        ev.ProducerDescription = Required(request.ProducerDescription, "a descrição do produtor", 10, 1000);

        var complement = (request.Complement ?? string.Empty).Trim();
        if (complement.Length > 100)
            throw new ValidationException("O complemento deve ter no máximo 100 caracteres.");
        ev.Complement = complement;

        var state = (request.State ?? string.Empty).Trim().ToUpperInvariant();
        if (state.Length != 2 || !state.All(char.IsAsciiLetter))
            throw new ValidationException("Informe o estado com a sigla de 2 letras (UF).");
        ev.State = state;

        var cepDigits = new string((request.Cep ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cepDigits.Length != 8)
            throw new ValidationException("CEP inválido.");
        ev.Cep = $"{cepDigits[..5]}-{cepDigits[5..]}";

        var startDate = ParseDate(request.StartDate, "de início");
        var startTime = ParseTime(request.StartTime, "de início");
        var endDate = ParseDate(request.EndDate, "de término");
        var endTime = ParseTime(request.EndTime, "de término");

        var start = startDate.ToDateTime(startTime);
        var end = endDate.ToDateTime(endTime);
        if (end <= start)
            throw new ValidationException("O término do evento deve ser depois do início.");

        ev.StartDate = startDate;
        ev.StartTime = startTime;
        ev.EndDate = endDate;
        ev.EndTime = endTime;

        // ── Ingressos ──
        // Ingressos que já venderam não podem ser removidos nem ter valor/quantidade reduzida.
        var incoming = request.Tickets ?? [];
        if (incoming.Count > MaxTicketsPerEvent)
            throw new ValidationException($"Um evento pode ter no máximo {MaxTicketsPerEvent} tipos de ingresso.");

        var feePercent = await GetFeePercentAsync();

        // Mapeia ingressos existentes pelo título para merge simplificado
        var existingByTitle = ev.Tickets.ToDictionary(t => t.Title, StringComparer.OrdinalIgnoreCase);
        var processedIds = new HashSet<Guid>();

        foreach (var dto in incoming)
        {
            var title = Required(dto.Title, "o título do ingresso", 2, 120);

            if (existingByTitle.TryGetValue(title, out var existing))
            {
                // Atualiza ingresso existente
                if (existing.SoldQuantity > 0)
                {
                    // Com vendas: só pode aumentar quantidade, não reduzir abaixo de vendidos
                    if (dto.Quantity < existing.SoldQuantity)
                        throw new ValidationException(
                            $"O ingresso \"{title}\" já vendeu {existing.SoldQuantity} unidades. A quantidade não pode ser menor que isso.");

                    existing.Quantity = dto.Quantity;
                    // Descrição e datas de venda podem mudar
                    existing.Description = (dto.Description ?? string.Empty).Trim();
                    var salesStart = ParseDate(dto.SalesStartDate, $"de início das vendas do ingresso \"{title}\"");
                    var salesEnd = ParseDate(dto.SalesEndDate, $"de fim das vendas do ingresso \"{title}\"");
                    if (salesEnd < salesStart)
                        throw new ValidationException($"O fim das vendas do ingresso \"{title}\" deve ser igual ou depois do início.");
                    if (salesEnd > endDate)
                        throw new ValidationException($"As vendas do ingresso \"{title}\" não podem terminar depois do término do evento.");
                    existing.SalesStartDate = salesStart;
                    existing.SalesEndDate = salesEnd;
                    // Valor e taxa NÃO mudam em ingresso com vendas (mantém snapshot)
                }
                else
                {
                    // Sem vendas: pode alterar tudo
                    var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
                    var rebuilt = BuildTicket(dto, feePercent, today, endDate);
                    existing.Title = rebuilt.Title;
                    existing.Quantity = rebuilt.Quantity;
                    existing.ReceiveAmount = rebuilt.ReceiveAmount;
                    existing.FeePercent = rebuilt.FeePercent;
                    existing.BuyerPrice = rebuilt.BuyerPrice;
                    existing.SalesStartDate = rebuilt.SalesStartDate;
                    existing.SalesEndDate = rebuilt.SalesEndDate;
                    existing.Description = rebuilt.Description;
                }
                processedIds.Add(existing.Id);
            }
            else
            {
                // Novo ingresso
                var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
                ev.Tickets.Add(BuildTicket(dto, feePercent, today, endDate));
            }
        }

        // Remove ingressos que não vieram no request E não têm vendas
        var toRemove = ev.Tickets.Where(t => !processedIds.Contains(t.Id) && t.SoldQuantity == 0 && !incoming.Any(i => i.Title.Equals(t.Title, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var r in toRemove)
            ev.Tickets.Remove(r);

        // Ingressos com vendas que foram omitidos do request continuam existindo (não removidos)

        await db.SaveChangesAsync();
        return ToDto(ev);
    }

    public async Task DeleteAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var ev = await db.TicketEvents.FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");

        // Evento com ingressos vendidos (ou pedidos pagos) não pode ser excluído: perderia vendas e check-ins.
        var hasSales = await db.EventTickets.AnyAsync(t => t.TicketEventId == id && t.SoldQuantity > 0)
            || await db.TicketOrders.AnyAsync(o => o.TicketEventId == id && o.Status == TicketOrderStatus.Paid);
        if (hasSales)
            throw new BusinessRuleException("Este evento já possui ingressos vendidos e não pode ser excluído.");

        db.TicketEvents.Remove(ev); // os ingressos saem junto (cascade no banco)
        await db.SaveChangesAsync();
    }

    // ── ingressos ──

    private static EventTicket BuildTicket(SaveEventTicketRequestDto t, decimal feePercent, DateOnly today, DateOnly eventEndDate)
    {
        var title = Required(t.Title, "o título do ingresso", 2, 120);
        var description = (t.Description ?? string.Empty).Trim();
        if (description.Length > 1000)
            throw new ValidationException("A descrição do ingresso deve ter no máximo 1000 caracteres.");

        if (t.Quantity < 1 || t.Quantity > MaxTicketQuantity)
            throw new ValidationException($"A quantidade do ingresso \"{title}\" deve estar entre 1 e {MaxTicketQuantity:N0}.");

        if (t.ReceiveAmount < 0 || t.ReceiveAmount > MaxReceiveAmount)
            throw new ValidationException($"O valor a receber do ingresso \"{title}\" é inválido.");

        var salesStart = ParseDate(t.SalesStartDate, $"de início das vendas do ingresso \"{title}\"");
        var salesEnd = ParseDate(t.SalesEndDate, $"de fim das vendas do ingresso \"{title}\"");
        if (salesStart < today)
            throw new ValidationException($"O início das vendas do ingresso \"{title}\" não pode estar no passado.");
        if (salesEnd < salesStart)
            throw new ValidationException($"O fim das vendas do ingresso \"{title}\" deve ser igual ou depois do início.");
        if (salesEnd > eventEndDate)
            throw new ValidationException($"As vendas do ingresso \"{title}\" não podem terminar depois do término do evento.");

        var receive = Math.Round(t.ReceiveAmount, 2, MidpointRounding.AwayFromZero);
        return new EventTicket
        {
            Title = title,
            Quantity = t.Quantity,
            ReceiveAmount = receive,
            FeePercent = feePercent,
            BuyerPrice = CalculateBuyerPrice(receive, feePercent),
            SalesStartDate = salesStart,
            SalesEndDate = salesEnd,
            Description = description
        };
    }

    /// <summary>Valor pago pelo comprador = valor a receber + taxa administrativa.</summary>
    public static decimal CalculateBuyerPrice(decimal receiveAmount, decimal feePercent)
        => Math.Round(receiveAmount * (1m + feePercent / 100m), 2, MidpointRounding.AwayFromZero);

    private async Task<decimal> GetFeePercentAsync()
    {
        var value = await db.Settings.AsNoTracking()
            .Where(s => s.Key == FeeSettingsKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var fee)
            || decimal.TryParse(value, out fee))
            return Math.Clamp(fee, 0m, MaxFeePercent);

        return DefaultFeePercent;
    }

    // ── helpers ──

    private static string Required(string? value, string label, int min, int max)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length < min)
            throw new ValidationException(min <= 1
                ? $"Informe {label}."
                : $"Informe {label} (mínimo de {min} caracteres).");
        if (text.Length > max)
            throw new ValidationException($"{Capitalize(label)} deve ter no máximo {max} caracteres.");
        return text;
    }

    private static string Capitalize(string label)
    {
        // "o nome do evento" → "Nome do evento"
        var trimmed = label.StartsWith("o ") || label.StartsWith("a ") ? label[2..] : label;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
    }

    private static DateOnly ParseDate(string? value, string which)
    {
        if (!DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ValidationException($"Data {which} inválida.");
        return date;
    }

    private static TimeOnly ParseTime(string? value, string which)
    {
        if (!TimeOnly.TryParseExact(value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            throw new ValidationException($"Horário {which} inválido.");
        return time;
    }

    /// <summary>Soma das reservas ativas (pedidos aguardando pagamento dentro do prazo) por ingresso.</summary>
    private async Task<Dictionary<Guid, int>> PendingReservationsAsync(IEnumerable<TicketEvent> events)
    {
        var ids = events.SelectMany(e => e.Tickets).Select(t => t.Id).ToList();
        if (ids.Count == 0) return [];
        var limit = DateTime.UtcNow - TicketOrderService.ReservationHold;
        return await db.TicketOrders.AsNoTracking()
            .Where(o => ids.Contains(o.EventTicketId)
                && o.Status == TicketOrderStatus.AwaitingPayment
                && o.CreatedAt > limit)
            .GroupBy(o => o.EventTicketId)
            .Select(g => new { g.Key, Quantity = g.Sum(o => o.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity);
    }

    private static TicketEventDto ToDto(TicketEvent e, Dictionary<Guid, int>? pending = null) => new(
        e.Id,
        e.Name,
        e.ImageUrl,
        e.Category,
        e.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture),
        e.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        e.EndDate.ToString(DateFormat, CultureInfo.InvariantCulture),
        e.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture),
        e.Description,
        e.VenueName,
        e.Cep,
        e.Street,
        e.Number,
        e.Complement,
        e.Neighborhood,
        e.City,
        e.State,
        e.ProducerName,
        e.ProducerDescription,
        e.CreatedAt,
        e.Tickets
            .OrderBy(t => t.CreatedAt)
            .Select(t => new EventTicketDto(
                t.Id,
                t.Title,
                t.Quantity,
                t.ReceiveAmount,
                t.FeePercent,
                t.BuyerPrice,
                t.SalesStartDate.ToString(DateFormat, CultureInfo.InvariantCulture),
                t.SalesEndDate.ToString(DateFormat, CultureInfo.InvariantCulture),
                t.Description,
                t.SoldQuantity,
                pending is not null && pending.TryGetValue(t.Id, out var reserved) ? reserved : 0,
                Math.Max(0, t.Quantity - t.SoldQuantity - (pending is not null && pending.TryGetValue(t.Id, out var r2) ? r2 : 0))))
            .ToList());
}
