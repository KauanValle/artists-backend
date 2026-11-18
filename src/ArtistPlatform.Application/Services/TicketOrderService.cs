using System.Net.Mail;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public record CreateTicketOrderRequestDto(
    Guid EventTicketId,
    string FullName,
    string Phone,
    string Email,
    string Cpf,
    int Quantity);

public record TicketOrderDto(
    Guid Id,
    Guid TicketEventId,
    Guid EventTicketId,
    string TicketTitle,
    string BuyerName,
    string BuyerEmail,
    int Quantity,
    decimal UnitPrice,
    decimal TotalAmount,
    string Status);

public interface ITicketOrderService
{
    /// <summary>Compra pública: reserva os ingressos e registra os dados do comprador.</summary>
    Task<TicketOrderDto> CreateAsync(Guid ticketEventId, CreateTicketOrderRequestDto request);
}

public class TicketOrderService(IAppDbContext db) : ITicketOrderService
{
    public const int MaxQuantityPerOrder = 10;

    public async Task<TicketOrderDto> CreateAsync(Guid ticketEventId, CreateTicketOrderRequestDto request)
    {
        var name = Normalize(request.FullName);
        if (name.Length < 3 || name.Length > 200 || !name.Contains(' '))
            throw new ValidationException("Informe o nome completo.");

        var phone = Digits(request.Phone);
        if (phone.StartsWith("55") && phone.Length > 11) phone = phone[2..];
        if (phone.Length is not (10 or 11))
            throw new ValidationException("Telefone inválido. Informe o DDD e o número.");

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
            throw new ValidationException("E-mail inválido.");

        var cpf = Digits(request.Cpf);
        if (!IsValidCpf(cpf))
            throw new ValidationException("CPF inválido.");

        if (request.Quantity < 1 || request.Quantity > MaxQuantityPerOrder)
            throw new ValidationException($"A quantidade deve estar entre 1 e {MaxQuantityPerOrder} ingressos por compra.");

        var ev = await db.TicketEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == ticketEventId)
            ?? throw new NotFoundException("Evento não encontrado.");

        var ticket = await db.EventTickets
            .FirstOrDefaultAsync(t => t.Id == request.EventTicketId && t.TicketEventId == ticketEventId)
            ?? throw new NotFoundException("Ingresso não encontrado.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        if (today < ticket.SalesStartDate)
            throw new BusinessRuleException("As vendas deste ingresso ainda não começaram.");
        if (today > ticket.SalesEndDate)
            throw new BusinessRuleException("As vendas deste ingresso foram encerradas.");

        var reserved = await db.TicketOrders
            .Where(o => o.EventTicketId == ticket.Id && o.Status != TicketOrderStatus.Cancelled)
            .SumAsync(o => (int?)o.Quantity) ?? 0;
        var available = ticket.Quantity - reserved;
        if (available <= 0)
            throw new BusinessRuleException("Ingressos esgotados.");
        if (request.Quantity > available)
            throw new BusinessRuleException(
                $"Restam apenas {available} ingresso{(available == 1 ? string.Empty : "s")} disponíve{(available == 1 ? "l" : "is")}.");

        var order = new TicketOrder
        {
            TicketEventId = ev.Id,
            EventTicketId = ticket.Id,
            BuyerName = name,
            BuyerPhone = phone,
            BuyerEmail = email,
            BuyerCpf = cpf,
            Quantity = request.Quantity,
            UnitPrice = ticket.BuyerPrice,
            TotalAmount = Math.Round(ticket.BuyerPrice * request.Quantity, 2, MidpointRounding.AwayFromZero),
            Status = TicketOrderStatus.AwaitingPayment,
        };
        db.TicketOrders.Add(order);
        await db.SaveChangesAsync();

        return new TicketOrderDto(order.Id, order.TicketEventId, order.EventTicketId, ticket.Title,
            order.BuyerName, order.BuyerEmail, order.Quantity, order.UnitPrice, order.TotalAmount, order.Status);
    }

    private static string Normalize(string? s) =>
        string.Join(' ', (s ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string Digits(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > 200) return false;
        try
        {
            var addr = new MailAddress(email);
            return addr.Address == email && addr.Host.Contains('.');
        }
        catch (FormatException) { return false; }
    }

    internal static bool IsValidCpf(string cpf)
    {
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1) return false;
        for (var t = 9; t < 11; t++)
        {
            var sum = 0;
            for (var i = 0; i < t; i++) sum += (cpf[i] - '0') * (t + 1 - i);
            var dv = sum * 10 % 11 % 10;
            if (cpf[t] - '0' != dv) return false;
        }
        return true;
    }
}
