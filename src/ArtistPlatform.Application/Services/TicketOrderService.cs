using System.Net.Mail;
using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
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

/// <summary>Pedido na consulta "acompanhar pedidos" (CPF + telefone).</summary>
public record TicketOrderSummaryDto(
    Guid Id,
    Guid TicketEventId,
    string EventName,
    string EventImageUrl,
    string EventStartDate,
    string EventStartTime,
    string Venue,
    string City,
    string State,
    string TicketTitle,
    int Quantity,
    decimal TotalAmount,
    string Status,
    /// <summary>Pedido aguardando pagamento cuja reserva já venceu.</summary>
    bool Expired,
    DateTime CreatedAt,
    DateTime? PaidAt,
    /// <summary>Só vem preenchido enquanto dá para pagar (aguardando pagamento e dentro do prazo).</summary>
    string? CheckoutUrl);

public interface ITicketOrderService
{
    /// <summary>Pedidos do comprador identificado por CPF + telefone (mais recentes primeiro).</summary>
    Task<List<TicketOrderSummaryDto>> LookupAsync(string cpf, string phone);

    /// <summary>Compra pública: reserva os ingressos e registra os dados do comprador.</summary>
    Task<TicketOrderDto> CreateAsync(Guid ticketEventId, CreateTicketOrderRequestDto request);

    /// <summary>Gera (ou reaproveita) o checkout do InfinityPay para o pedido. Ingresso gratuito é confirmado direto.</summary>
    Task<TicketCheckoutDto> StartCheckoutAsync(Guid ticketEventId, Guid orderId);

    /// <summary>
    /// Volta do checkout (redirect_url traz order_nsu, transaction_nsu e slug): confirma o pagamento
    /// consultando o InfinityPay — os parâmetros do navegador nunca valem por si só.
    /// </summary>
    Task<TicketReturnDto> VerifyReturnAsync(string orderNsu, string? transactionNsu, string? slug);

    /// <summary>Webhook do InfinityPay. Retorna null se a referência não for de um pedido de ingresso.</summary>
    Task<WebhookResultDto?> HandleWebhookAsync(Guid orderRef, string body);
}

/// <summary>Resultado do checkout: URL do InfinityPay, ou Paid = true quando o ingresso é gratuito.</summary>
public record TicketCheckoutDto(string? CheckoutUrl, bool Paid);

/// <summary>Resultado da verificação na volta do checkout: Found = o retorno é de um pedido de ingresso.</summary>
public record TicketReturnDto(bool Found, bool Paid);

public class TicketOrderService(IAppDbContext db, IInfinityPayClient infinityPay) : ITicketOrderService
{
    public const int MaxQuantityPerOrder = 10;

    /// <summary>Tempo que um pedido aguardando pagamento mantém os ingressos reservados.</summary>
    public static readonly TimeSpan ReservationHold = TimeSpan.FromMinutes(30);

    private static readonly string[] ConfirmationMarkers =
        ["paid", "confirmed", "succeeded", "approved", "captured", "completed", "settled", "authorized"];

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

        var available = ticket.Quantity - ticket.SoldQuantity - await PendingAsync(ticket.Id);
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

        // Conferência após gravar: se duas compras simultâneas passaram da checagem acima, o excesso
        // é detectado aqui e o pedido é desfeito (nunca vende além do total).
        var sold = await db.EventTickets.AsNoTracking().Where(t => t.Id == ticket.Id).Select(t => t.SoldQuantity).FirstAsync();
        if (sold + await PendingAsync(ticket.Id) > ticket.Quantity)
        {
            db.TicketOrders.Remove(order);
            await db.SaveChangesAsync();
            throw new BusinessRuleException("Os ingressos acabaram enquanto você finalizava a compra. Tente novamente.");
        }

        return new TicketOrderDto(order.Id, order.TicketEventId, order.EventTicketId, ticket.Title,
            order.BuyerName, order.BuyerEmail, order.Quantity, order.UnitPrice, order.TotalAmount, order.Status);
    }

    public async Task<TicketReturnDto> VerifyReturnAsync(string orderNsu, string? transactionNsu, string? slug)
    {
        if (!Guid.TryParse(orderNsu, out var orderId)) return new TicketReturnDto(false, false);
        var order = await db.TicketOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId);
        if (order is null) return new TicketReturnDto(false, false);
        if (order.Status == TicketOrderStatus.Paid) return new TicketReturnDto(true, true);
        if (order.Status == TicketOrderStatus.Cancelled) return new TicketReturnDto(true, false);

        bool paid;
        try
        {
            paid = await infinityPay.IsPaidAsync(orderNsu, transactionNsu, slug);
        }
        catch (Exception)
        {
            return new TicketReturnDto(true, false);
        }
        if (paid) await MarkPaidAsync(order);
        return new TicketReturnDto(true, paid);
    }

    private async Task<bool> TryConfirmWithGatewayAsync(TicketOrder order)
    {
        try
        {
            // Pedidos novos: order_nsu = id do pedido. Pedidos antigos: tenta o código devolvido pelo InfinityPay ao criar o link.
            var paid = await infinityPay.IsPaidAsync(order.Id.ToString());
            if (!paid && !string.IsNullOrWhiteSpace(order.TransactionId))
                paid = await infinityPay.IsPaidAsync(order.TransactionId);
            if (!paid) return false;
        }
        catch (Exception)
        {
            return false; // gateway indisponível: mantém o status atual
        }
        await MarkPaidAsync(order);
        return true;
    }

    /// <summary>Quantidade reservada por pedidos aguardando pagamento ainda dentro do prazo.</summary>
    private async Task<int> PendingAsync(Guid eventTicketId)
    {
        var limit = DateTime.UtcNow - ReservationHold;
        return await db.TicketOrders.AsNoTracking()
            .Where(o => o.EventTicketId == eventTicketId
                && o.Status == TicketOrderStatus.AwaitingPayment
                && o.CreatedAt > limit)
            .SumAsync(o => (int?)o.Quantity) ?? 0;
    }

    /// <summary>
    /// Confirma o pagamento e baixa o estoque. Atômico e idempotente: só o primeiro chamador que muda o
    /// status de não-pago para pago incrementa SoldQuantity; reenvios do webhook não duplicam a baixa.
    /// </summary>
    private async Task<bool> MarkPaidAsync(TicketOrder order)
    {
        var now = DateTime.UtcNow;
        var changed = await db.TicketOrders
            .Where(o => o.Id == order.Id && o.Status != TicketOrderStatus.Paid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, TicketOrderStatus.Paid)
                .SetProperty(o => o.PaidAt, now)
                .SetProperty(o => o.UpdatedAt, now));
        if (changed == 0) return false;

        await db.EventTickets
            .Where(t => t.Id == order.EventTicketId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SoldQuantity, t => t.SoldQuantity + order.Quantity));
        return true;
    }

    public async Task<List<TicketOrderSummaryDto>> LookupAsync(string cpf, string phone)
    {
        var cpfDigits = Digits(cpf);
        if (!IsValidCpf(cpfDigits))
            throw new ValidationException("CPF inválido.");

        var phoneDigits = Digits(phone);
        if (phoneDigits.StartsWith("55") && phoneDigits.Length > 11) phoneDigits = phoneDigits[2..];
        if (phoneDigits.Length is not (10 or 11))
            throw new ValidationException("Telefone inválido. Informe o DDD e o número.");

        var rows = await (
            from o in db.TicketOrders.AsNoTracking()
            join t in db.EventTickets.AsNoTracking() on o.EventTicketId equals t.Id
            join e in db.TicketEvents.AsNoTracking() on o.TicketEventId equals e.Id
            where o.BuyerCpf == cpfDigits && o.BuyerPhone == phoneDigits
            orderby o.CreatedAt descending
            select new { Order = o, TicketTitle = t.Title, Event = e }).Take(100).ToListAsync();

        // Plano B do webhook: pedidos ainda "aguardando" são conferidos direto no InfinityPay, então
        // abrir "Acompanhar pedidos" já corrige o status mesmo que o webhook não tenha chegado.
        foreach (var r in rows.Where(r => r.Order.Status == TicketOrderStatus.AwaitingPayment
                                          && !string.IsNullOrWhiteSpace(r.Order.CheckoutUrl)).Take(5))
        {
            if (await TryConfirmWithGatewayAsync(r.Order))
            {
                r.Order.Status = TicketOrderStatus.Paid;
                r.Order.PaidAt = DateTime.UtcNow;
            }
        }

        var limit = DateTime.UtcNow - ReservationHold;
        return rows.Select(r =>
        {
            var awaiting = r.Order.Status == TicketOrderStatus.AwaitingPayment;
            var expired = awaiting && r.Order.CreatedAt <= limit;
            return new TicketOrderSummaryDto(
                r.Order.Id,
                r.Event.Id,
                r.Event.Name,
                r.Event.ImageUrl,
                r.Event.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                r.Event.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                r.Event.VenueName,
                r.Event.City,
                r.Event.State,
                r.TicketTitle,
                r.Order.Quantity,
                r.Order.TotalAmount,
                r.Order.Status,
                expired,
                r.Order.CreatedAt,
                r.Order.PaidAt,
                awaiting && !expired ? r.Order.CheckoutUrl : null);
        }).ToList();
    }

    public async Task<TicketCheckoutDto> StartCheckoutAsync(Guid ticketEventId, Guid orderId)
    {
        var order = await db.TicketOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.TicketEventId == ticketEventId)
            ?? throw new NotFoundException("Pedido não encontrado.");

        if (order.Status == TicketOrderStatus.Paid)
            return new TicketCheckoutDto(null, true);
        if (order.Status == TicketOrderStatus.Cancelled)
            throw new BusinessRuleException("Este pedido foi cancelado.");

        // Ingresso gratuito: sem gateway, o pedido é confirmado na hora.
        if (order.TotalAmount <= 0)
        {
            await MarkPaidAsync(order);
            return new TicketCheckoutDto(null, true);
        }

        if (!string.IsNullOrWhiteSpace(order.CheckoutUrl))
            return new TicketCheckoutDto(order.CheckoutUrl, false);

        if (order.CreatedAt < DateTime.UtcNow - ReservationHold)
            throw new BusinessRuleException("A reserva deste pedido expirou. Faça uma nova compra.");

        var info = await (
            from t in db.EventTickets.AsNoTracking()
            join e in db.TicketEvents.AsNoTracking() on t.TicketEventId equals e.Id
            where t.Id == order.EventTicketId
            select new { t.Title, EventName = e.Name }).FirstAsync();

        var description = $"{order.Quantity}x {info.Title} - {info.EventName}";
        if (description.Length > 200) description = description[..200];

        var item = new InfinityPayItem(Convert.ToInt32(Math.Round(order.TotalAmount * 100, MidpointRounding.AwayFromZero)), description);
        var link = await infinityPay.CreateLinkAsync(order.Id, [item]);

        order.TransactionId = link.TransactionId;
        order.CheckoutUrl = link.CheckoutUrl;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return new TicketCheckoutDto(link.CheckoutUrl, false);
    }

    public async Task<WebhookResultDto?> HandleWebhookAsync(Guid orderRef, string body)
    {
        var order = await db.TicketOrders.FirstOrDefaultAsync(o => o.Id == orderRef);
        if (order is null) return null;

        if (order.Status == TicketOrderStatus.Paid)
            return new WebhookResultDto(true, "Pedido já confirmado anteriormente.");

        var content = body.ToLowerInvariant();
        if (!ConfirmationMarkers.Any(m => content.Contains(m)))
            return new WebhookResultDto(false, "Evento não indica pagamento confirmado — ignorado.");

        // Pagamento confirmado vale mesmo que a reserva tenha expirado (o dinheiro já entrou).
        await MarkPaidAsync(order);
        return new WebhookResultDto(true, "Pagamento do ingresso confirmado.");
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
