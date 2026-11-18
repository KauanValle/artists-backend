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
    DateTime CreatedAt);

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
    string ProducerDescription);

// ─────────────────────────── Interface ───────────────────────────

public interface ITicketEventService
{
    /// <summary>Eventos do usuário logado (mais recentes primeiro).</summary>
    Task<List<TicketEventDto>> ListAsync();
    Task<TicketEventDto> GetAsync(Guid id);
    Task<TicketEventDto> CreateAsync(SaveTicketEventRequestDto request);
    Task DeleteAsync(Guid id);
}

// ─────────────────────────── Implementação ───────────────────────────

public class TicketEventService(IAppDbContext db, ICurrentUserService currentUser) : ITicketEventService
{
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly string[] TimeFormats = ["HH:mm", "HH:mm:ss"];

    public async Task<List<TicketEventDto>> ListAsync()
    {
        var userId = currentUser.EnsureAuthenticated();
        var events = await db.TicketEvents.AsNoTracking()
            .Where(e => e.OwnerUserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return events.Select(ToDto).ToList();
    }

    public async Task<TicketEventDto> GetAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var ev = await db.TicketEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");
        return ToDto(ev);
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

        db.TicketEvents.Add(ev);
        await db.SaveChangesAsync();
        return ToDto(ev);
    }

    public async Task DeleteAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var ev = await db.TicketEvents.FirstOrDefaultAsync(e => e.Id == id && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");
        db.TicketEvents.Remove(ev);
        await db.SaveChangesAsync();
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

    private static TicketEventDto ToDto(TicketEvent e) => new(
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
        e.CreatedAt);
}
