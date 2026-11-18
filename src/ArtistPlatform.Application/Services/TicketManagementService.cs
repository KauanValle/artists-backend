using System.Security.Cryptography;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

// ─────────────────────────── Códigos ───────────────────────────

/// <summary>Geração e normalização dos códigos de ingresso e de portaria (alfabeto sem caracteres ambíguos).</summary>
public static class TicketCodes
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ"; // sem 0/O, 1/I/L

    public const int TicketCodeLength = 16;
    public const int GateCodeLength = 12;

    public static string Generate(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>Maiúsculas, só letras e números (aceita código digitado com hífen/espaço).</summary>
    public static string Normalize(string? raw) =>
        new((raw ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}

// ─────────────────────────── DTOs ───────────────────────────

public record TicketTypeStatsDto(
    Guid TicketId,
    string Title,
    int Quantity,
    int Sold,
    int Reserved,
    int Available,
    int CheckedIn,
    decimal BuyerPrice,
    decimal ReceiveAmount,
    /// <summary>Total pago pelos compradores (vendidos × valor com taxas).</summary>
    decimal GrossRevenue,
    /// <summary>O que o produtor recebe (vendidos × valor a receber).</summary>
    decimal NetRevenue);

public record TicketEventStatsDto(
    Guid EventId,
    string Name,
    int TotalCapacity,
    int TotalSold,
    int TotalReserved,
    int TotalCheckedIn,
    int PendingOrders,
    decimal GrossRevenue,
    decimal NetRevenue,
    List<TicketTypeStatsDto> Types);

public record EventOrderDto(
    Guid Id,
    string BuyerName,
    string BuyerEmail,
    string BuyerPhone,
    string BuyerCpf,
    string TicketTitle,
    int Quantity,
    decimal TotalAmount,
    string Status,
    bool Expired,
    DateTime CreatedAt,
    DateTime? PaidAt,
    int CheckedIn);

public record CheckInLogDto(
    Guid TicketId,
    string Code,
    string TicketTitle,
    string BuyerName,
    int Sequence,
    int Quantity,
    DateTime CheckedInAt,
    string CheckedInBy);

/// <summary>Status: Valid | AlreadyUsed | WrongEvent | NotFound | Cancelled.</summary>
public record CheckInResultDto(
    string Status,
    string Message,
    string? TicketTitle,
    string? BuyerName,
    int? Sequence,
    int? Quantity,
    DateTime? CheckedInAt,
    string? CheckedInBy,
    int EnteredCount,
    int TotalSold);

public record GateAccessDto(Guid Id, string Name, string Code, DateTime CreatedAt, DateTime? RevokedAt, int CheckIns);

public record GateSessionDto(
    Guid EventId,
    string EventName,
    string GateName,
    string StartDate,
    string StartTime,
    string Venue,
    int EnteredCount,
    int TotalSold);

// ─────────────────────────── Interface ───────────────────────────

public interface ITicketManagementService
{
    // Produtor (autenticado, dono do evento)
    Task<TicketEventStatsDto> GetStatsAsync(Guid eventId);
    Task<List<EventOrderDto>> ListOrdersAsync(Guid eventId);
    Task<CheckInResultDto> CheckInAsOwnerAsync(Guid eventId, string ticketCode);
    Task<List<CheckInLogDto>> ListCheckInsAsync(Guid eventId);
    Task UndoCheckInAsync(Guid eventId, Guid issuedTicketId);
    Task<List<GateAccessDto>> ListGatesAsync(Guid eventId);
    Task<GateAccessDto> CreateGateAsync(Guid eventId, string name);
    Task RevokeGateAsync(Guid eventId, Guid gateId);

    // Portaria (sem conta, identificada pelo código do acesso)
    Task<GateSessionDto> OpenGateAsync(string gateCode);
    Task<CheckInResultDto> CheckInAtGateAsync(string gateCode, string ticketCode);
}

// ─────────────────────────── Implementação ───────────────────────────

public class TicketManagementService(IAppDbContext db, ICurrentUserService currentUser) : ITicketManagementService
{
    private const int MaxGatesPerEvent = 20;
    private const string OwnerLabel = "Produtor";

    // ── produtor ──

    private async Task<TicketEvent> LoadOwnedEventAsync(Guid eventId)
    {
        var userId = currentUser.EnsureAuthenticated();
        return await db.TicketEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.OwnerUserId == userId)
            ?? throw new NotFoundException("Evento não encontrado.");
    }

    public async Task<TicketEventStatsDto> GetStatsAsync(Guid eventId)
    {
        var ev = await LoadOwnedEventAsync(eventId);

        var tickets = await db.EventTickets.AsNoTracking()
            .Where(t => t.TicketEventId == eventId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var limit = DateTime.UtcNow - TicketOrderService.ReservationHold;
        var reservedByTicket = await db.TicketOrders.AsNoTracking()
            .Where(o => o.TicketEventId == eventId
                && o.Status == TicketOrderStatus.AwaitingPayment
                && o.CreatedAt > limit)
            .GroupBy(o => o.EventTicketId)
            .Select(g => new { g.Key, Quantity = g.Sum(o => o.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Quantity);

        var pendingOrders = await db.TicketOrders.AsNoTracking()
            .CountAsync(o => o.TicketEventId == eventId
                && o.Status == TicketOrderStatus.AwaitingPayment
                && o.CreatedAt > limit);

        var checkedInByTicket = await db.IssuedTickets.AsNoTracking()
            .Where(t => t.TicketEventId == eventId && t.CheckedInAt != null)
            .GroupBy(t => t.EventTicketId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var types = tickets.Select(t =>
        {
            var reserved = reservedByTicket.GetValueOrDefault(t.Id);
            return new TicketTypeStatsDto(
                t.Id, t.Title, t.Quantity, t.SoldQuantity, reserved,
                Math.Max(0, t.Quantity - t.SoldQuantity - reserved),
                checkedInByTicket.GetValueOrDefault(t.Id),
                t.BuyerPrice, t.ReceiveAmount,
                t.SoldQuantity * t.BuyerPrice,
                t.SoldQuantity * t.ReceiveAmount);
        }).ToList();

        return new TicketEventStatsDto(
            ev.Id, ev.Name,
            types.Sum(t => t.Quantity),
            types.Sum(t => t.Sold),
            types.Sum(t => t.Reserved),
            types.Sum(t => t.CheckedIn),
            pendingOrders,
            types.Sum(t => t.GrossRevenue),
            types.Sum(t => t.NetRevenue),
            types);
    }

    public async Task<List<EventOrderDto>> ListOrdersAsync(Guid eventId)
    {
        await LoadOwnedEventAsync(eventId);

        var rows = await (
            from o in db.TicketOrders.AsNoTracking()
            join t in db.EventTickets.AsNoTracking() on o.EventTicketId equals t.Id
            where o.TicketEventId == eventId
            orderby o.CreatedAt descending
            select new { Order = o, TicketTitle = t.Title }).Take(1000).ToListAsync();

        var checkedIn = await db.IssuedTickets.AsNoTracking()
            .Where(t => t.TicketEventId == eventId && t.CheckedInAt != null)
            .GroupBy(t => t.TicketOrderId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var limit = DateTime.UtcNow - TicketOrderService.ReservationHold;
        return rows.Select(r => new EventOrderDto(
            r.Order.Id, r.Order.BuyerName, r.Order.BuyerEmail, r.Order.BuyerPhone, r.Order.BuyerCpf,
            r.TicketTitle, r.Order.Quantity, r.Order.TotalAmount, r.Order.Status,
            r.Order.Status == TicketOrderStatus.AwaitingPayment && r.Order.CreatedAt <= limit,
            r.Order.CreatedAt, r.Order.PaidAt, checkedIn.GetValueOrDefault(r.Order.Id))).ToList();
    }

    public async Task<CheckInResultDto> CheckInAsOwnerAsync(Guid eventId, string ticketCode)
    {
        await LoadOwnedEventAsync(eventId);
        return await CheckInCoreAsync(eventId, ticketCode, OwnerLabel, null);
    }

    public async Task<List<CheckInLogDto>> ListCheckInsAsync(Guid eventId)
    {
        await LoadOwnedEventAsync(eventId);
        return await ListCheckInsCoreAsync(eventId);
    }

    public async Task UndoCheckInAsync(Guid eventId, Guid issuedTicketId)
    {
        await LoadOwnedEventAsync(eventId);
        var changed = await db.IssuedTickets
            .Where(t => t.Id == issuedTicketId && t.TicketEventId == eventId && t.CheckedInAt != null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CheckedInAt, (DateTime?)null)
                .SetProperty(t => t.CheckedInBy, (string?)null)
                .SetProperty(t => t.CheckedInGateId, (Guid?)null));
        if (changed == 0)
            throw new NotFoundException("Entrada não encontrada ou já desfeita.");
    }

    public async Task<List<GateAccessDto>> ListGatesAsync(Guid eventId)
    {
        await LoadOwnedEventAsync(eventId);
        var gates = await db.GateAccesses.AsNoTracking()
            .Where(g => g.TicketEventId == eventId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
        var counts = await db.IssuedTickets.AsNoTracking()
            .Where(t => t.TicketEventId == eventId && t.CheckedInGateId != null)
            .GroupBy(t => t.CheckedInGateId!.Value)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        return gates.Select(g => new GateAccessDto(g.Id, g.Name, g.Code, g.CreatedAt, g.RevokedAt, counts.GetValueOrDefault(g.Id))).ToList();
    }

    public async Task<GateAccessDto> CreateGateAsync(Guid eventId, string name)
    {
        await LoadOwnedEventAsync(eventId);

        name = (name ?? string.Empty).Trim();
        if (name.Length < 2 || name.Length > 120)
            throw new ValidationException("Informe um nome para a portaria (2 a 120 caracteres).");

        var active = await db.GateAccesses.CountAsync(g => g.TicketEventId == eventId && g.RevokedAt == null);
        if (active >= MaxGatesPerEvent)
            throw new BusinessRuleException($"Um evento pode ter no máximo {MaxGatesPerEvent} acessos de portaria ativos.");

        var gate = new GateAccess
        {
            TicketEventId = eventId,
            Name = name,
            Code = TicketCodes.Generate(TicketCodes.GateCodeLength)
        };
        db.GateAccesses.Add(gate);
        await db.SaveChangesAsync();
        return new GateAccessDto(gate.Id, gate.Name, gate.Code, gate.CreatedAt, gate.RevokedAt, 0);
    }

    public async Task RevokeGateAsync(Guid eventId, Guid gateId)
    {
        await LoadOwnedEventAsync(eventId);
        var gate = await db.GateAccesses.FirstOrDefaultAsync(g => g.Id == gateId && g.TicketEventId == eventId)
            ?? throw new NotFoundException("Acesso de portaria não encontrado.");
        if (gate.RevokedAt is not null) return;
        gate.RevokedAt = DateTime.UtcNow;
        gate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── portaria ──

    private async Task<GateAccess> LoadGateAsync(string gateCode)
    {
        var code = TicketCodes.Normalize(gateCode);
        return await db.GateAccesses.AsNoTracking().FirstOrDefaultAsync(g => g.Code == code && g.RevokedAt == null)
            ?? throw new ValidationException("Código de portaria inválido ou revogado.");
    }

    public async Task<GateSessionDto> OpenGateAsync(string gateCode)
    {
        var gate = await LoadGateAsync(gateCode);
        var ev = await db.TicketEvents.AsNoTracking().FirstAsync(e => e.Id == gate.TicketEventId);
        var (entered, sold) = await CountsAsync(ev.Id);
        return new GateSessionDto(
            ev.Id, ev.Name, gate.Name,
            ev.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ev.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            ev.VenueName, entered, sold);
    }

    public async Task<CheckInResultDto> CheckInAtGateAsync(string gateCode, string ticketCode)
    {
        var gate = await LoadGateAsync(gateCode);
        return await CheckInCoreAsync(gate.TicketEventId, ticketCode, gate.Name, gate.Id);
    }

    // ── núcleo ──

    private async Task<(int Entered, int Sold)> CountsAsync(Guid eventId)
    {
        var entered = await db.IssuedTickets.AsNoTracking().CountAsync(t => t.TicketEventId == eventId && t.CheckedInAt != null);
        var sold = await db.EventTickets.AsNoTracking().Where(t => t.TicketEventId == eventId).SumAsync(t => (int?)t.SoldQuantity) ?? 0;
        return (entered, sold);
    }

    /// <summary>
    /// Valida o ingresso e registra a entrada de forma atômica: dois porteiros lendo o mesmo QR code ao mesmo
    /// tempo — só um recebe "Valid", o outro recebe "AlreadyUsed".
    /// </summary>
    private async Task<CheckInResultDto> CheckInCoreAsync(Guid eventId, string rawCode, string by, Guid? gateId)
    {
        var code = TicketCodes.Normalize(rawCode);

        async Task<CheckInResultDto> Result(string status, string message, IssuedTicket? issued = null, string? title = null,
            string? buyer = null, int? quantity = null)
        {
            var (entered, sold) = await CountsAsync(eventId);
            return new CheckInResultDto(status, message, title, buyer, issued?.Sequence, quantity,
                issued?.CheckedInAt, issued?.CheckedInBy, entered, sold);
        }

        if (code.Length != TicketCodes.TicketCodeLength)
            return await Result("NotFound", "Código inválido. Este QR code não é um ingresso da Reprissa.");

        var row = await (
            from t in db.IssuedTickets.AsNoTracking()
            join o in db.TicketOrders.AsNoTracking() on t.TicketOrderId equals o.Id
            join et in db.EventTickets.AsNoTracking() on t.EventTicketId equals et.Id
            where t.Code == code
            select new { Ticket = t, Order = o, Title = et.Title }).FirstOrDefaultAsync();

        if (row is null)
            return await Result("NotFound", "Ingresso não encontrado.");

        if (row.Ticket.TicketEventId != eventId)
            return await Result("WrongEvent", "Este ingresso é de outro evento.");

        if (row.Order.Status != TicketOrderStatus.Paid)
            return await Result("Cancelled", "O pedido deste ingresso não está pago.", row.Ticket, row.Title, row.Order.BuyerName, row.Order.Quantity);

        var now = DateTime.UtcNow;
        var changed = await db.IssuedTickets
            .Where(t => t.Id == row.Ticket.Id && t.CheckedInAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CheckedInAt, (DateTime?)now)
                .SetProperty(t => t.CheckedInBy, (string?)by)
                .SetProperty(t => t.CheckedInGateId, gateId));

        if (changed == 1)
        {
            row.Ticket.CheckedInAt = now;
            row.Ticket.CheckedInBy = by;
            return await Result("Valid", "Entrada liberada.", row.Ticket, row.Title, row.Order.BuyerName, row.Order.Quantity);
        }

        var current = await db.IssuedTickets.AsNoTracking().FirstAsync(t => t.Id == row.Ticket.Id);
        return await Result("AlreadyUsed", "Este ingresso já foi utilizado.", current, row.Title, row.Order.BuyerName, row.Order.Quantity);
    }

    private async Task<List<CheckInLogDto>> ListCheckInsCoreAsync(Guid eventId)
    {
        var rows = await (
            from t in db.IssuedTickets.AsNoTracking()
            join o in db.TicketOrders.AsNoTracking() on t.TicketOrderId equals o.Id
            join et in db.EventTickets.AsNoTracking() on t.EventTicketId equals et.Id
            where t.TicketEventId == eventId && t.CheckedInAt != null
            orderby t.CheckedInAt descending
            select new { Ticket = t, o.BuyerName, o.Quantity, et.Title }).Take(100).ToListAsync();

        return rows.Select(r => new CheckInLogDto(
            r.Ticket.Id, r.Ticket.Code, r.Title, r.BuyerName, r.Ticket.Sequence, r.Quantity,
            r.Ticket.CheckedInAt!.Value, r.Ticket.CheckedInBy ?? string.Empty)).ToList();
    }
}
