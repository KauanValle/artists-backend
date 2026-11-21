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
    string? CheckoutUrl,
    // Dados para o PDF do ingresso (o comprador já se identificou com CPF + telefone).
    string EventEndDate,
    string EventEndTime,
    string Street,
    string Number,
    string Complement,
    string Neighborhood,
    string Cep,
    string ProducerName,
    string BuyerName,
    string BuyerEmail,
    string BuyerPhone,
    string BuyerCpf,
    /// <summary>Ingressos individuais (código do QR code) — só existem para pedidos pagos.</summary>
    List<IssuedTicketDto> Tickets);

public record IssuedTicketDto(int Sequence, string Code, DateTime? CheckedInAt);

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
public record TicketReturnDto(bool Found, bool Paid, TicketOrderSummaryDto? OrderSummary = null);

public class TicketOrderService(IAppDbContext db, IInfinityPayClient infinityPay, IEmailService emailService) : ITicketOrderService
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
        if (order.Status == TicketOrderStatus.Paid)
            return new TicketReturnDto(true, true, await BuildOrderSummaryAsync(order));
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
        if (paid)
        {
            // Recarrega o pedido para pegar PaidAt atualizado
            order = (await db.TicketOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId))!;
            return new TicketReturnDto(true, true, await BuildOrderSummaryAsync(order));
        }
        return new TicketReturnDto(true, false);
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

    /// <summary>Monta o TicketOrderSummaryDto para um único pedido (usado no retorno do verify).</summary>
    private async Task<TicketOrderSummaryDto> BuildOrderSummaryAsync(TicketOrder order)
    {
        var row = await (
            from t in db.EventTickets.AsNoTracking()
            join e in db.TicketEvents.AsNoTracking() on t.TicketEventId equals e.Id
            where t.Id == order.EventTicketId
            select new { TicketTitle = t.Title, Event = e }).FirstAsync();

        await EnsureIssuedAsync(order);
        var tickets = await db.IssuedTickets.AsNoTracking()
            .Where(i => i.TicketOrderId == order.Id)
            .OrderBy(i => i.Sequence)
            .Select(i => new IssuedTicketDto(i.Sequence, i.Code, i.CheckedInAt))
            .ToListAsync();

        var awaiting = order.Status == TicketOrderStatus.AwaitingPayment;
        var limit = DateTime.UtcNow - ReservationHold;
        var expired = awaiting && order.CreatedAt <= limit;

        return new TicketOrderSummaryDto(
            order.Id,
            row.Event.Id,
            row.Event.Name,
            row.Event.ImageUrl,
            row.Event.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            row.Event.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            row.Event.VenueName,
            row.Event.City,
            row.Event.State,
            row.TicketTitle,
            order.Quantity,
            order.TotalAmount,
            order.Status,
            expired,
            order.CreatedAt,
            order.PaidAt,
            awaiting && !expired ? order.CheckoutUrl : null,
            row.Event.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            row.Event.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            row.Event.Street,
            row.Event.Number,
            row.Event.Complement ?? string.Empty,
            row.Event.Neighborhood,
            row.Event.Cep,
            row.Event.ProducerName,
            order.BuyerName,
            order.BuyerEmail,
            order.BuyerPhone,
            order.BuyerCpf,
            tickets);
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

        await EnsureIssuedAsync(order);

        // Envia email de confirmação com os códigos do ingresso
        try { await SendTicketConfirmationEmailAsync(order); }
        catch { /* falha no email não impede o fluxo principal */ }

        return true;
    }

    /// <summary>
    /// Envia email de confirmação do ingresso com os códigos QR após pagamento.
    /// Chamado automaticamente pelo webhook e pelo verify-return.
    /// </summary>
    private async Task SendTicketConfirmationEmailAsync(TicketOrder order)
    {
        var row = await (
            from t in db.EventTickets.AsNoTracking()
            join e in db.TicketEvents.AsNoTracking() on t.TicketEventId equals e.Id
            where t.Id == order.EventTicketId
            select new { TicketTitle = t.Title, Event = e }).FirstOrDefaultAsync();
        if (row is null) return;

        var tickets = await db.IssuedTickets.AsNoTracking()
            .Where(i => i.TicketOrderId == order.Id)
            .OrderBy(i => i.Sequence)
            .ToListAsync();

        var ticketCodesHtml = string.Join("", tickets.Select(t =>
        {
            var formatted = string.Join("-", Enumerable.Range(0, (int)Math.Ceiling(t.Code.Length / 4.0))
                .Select(i => t.Code.Substring(i * 4, Math.Min(4, t.Code.Length - i * 4))));
            return $"<div style='background:#f3f4f6;border-radius:8px;padding:12px 16px;margin:4px 0;font-family:monospace;font-size:16px;letter-spacing:1px;text-align:center;'>{formatted}</div>";
        }));

        var eventDate = row.Event.StartDate.ToString("dd/MM/yyyy");
        var eventTime = row.Event.StartTime.ToString("HH:mm");

        var html = $"""
        <h1>Pagamento confirmado! 🎉</h1>
        <p>Olá, <span class="highlight">{order.BuyerName}</span>!</p>
        <p>Seu pagamento para o evento <strong>{row.Event.Name}</strong> foi confirmado com sucesso.</p>

        <div style="background:#f9fafb;border:1px solid #e5e7eb;border-radius:12px;padding:20px;margin:20px 0;">
          <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Evento</p>
          <p style="margin:0 0 12px;font-weight:700;font-size:16px;">{row.Event.Name}</p>
          <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Data</p>
          <p style="margin:0 0 12px;font-weight:600;">{eventDate} às {eventTime}</p>
          <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Local</p>
          <p style="margin:0 0 12px;font-weight:600;">{row.Event.VenueName} · {row.Event.City}/{row.Event.State}</p>
          <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Ingresso</p>
          <p style="margin:0;font-weight:600;">{order.Quantity}x {row.TicketTitle}</p>
        </div>

        <h2 style="font-size:16px;margin:24px 0 8px;">Seus códigos de ingresso</h2>
        {ticketCodesHtml}

        <p style="margin-top:20px;">Para baixar seu ingresso em PDF com o QR code, acesse:</p>
        <div style="text-align:center;margin:16px 0;">
          <a href="https://reprissa.com.br/acompanhar-pedidos" style="display:inline-block;padding:12px 32px;background:linear-gradient(135deg,#8b5cf6,#7c3aed);color:white;text-decoration:none;border-radius:8px;font-weight:600;">Acompanhar meus pedidos</a>
        </div>

        <p style="color:#9ca3af;font-size:13px;margin-top:24px;">Apresente o QR code na entrada do evento junto com um documento com foto.</p>
        """;

        var fullHtml = WrapInLayout(html, $"Ingresso confirmado - {row.Event.Name}");

        await emailService.SendAsync(new EmailMessage
        {
            To = order.BuyerEmail,
            Subject = $"🎉 Ingresso confirmado: {row.Event.Name}",
            HtmlBody = fullHtml,
            Bcc = ["geral@reprissa.com.br"],
        });
    }

    private static string WrapInLayout(string content, string title) =>
        """
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>TITLE_PLACEHOLDER</title>
          <style>
            body { margin: 0; padding: 0; background-color: #f4f4f7; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
            .wrapper { max-width: 600px; margin: 0 auto; padding: 32px 16px; }
            .card { background: #ffffff; border-radius: 12px; padding: 40px 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.08); }
            .logo { text-align: center; margin-bottom: 32px; }
            .logo span { font-size: 24px; font-weight: 800; background: linear-gradient(135deg, #8b5cf6, #7c3aed, #6d28d9); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text; letter-spacing: -0.5px; }
            h1 { color: #1a1a2e; font-size: 22px; font-weight: 700; margin: 0 0 16px; }
            h2 { color: #1a1a2e; }
            p { color: #4a4a68; font-size: 15px; line-height: 1.7; margin: 0 0 16px; }
            .highlight { color: #7c3aed; font-weight: 600; }
            .footer { text-align: center; margin-top: 32px; padding-top: 24px; border-top: 1px solid #e8e8ed; }
            .footer p { color: #9ca3af; font-size: 12px; line-height: 1.6; }
            .footer a { color: #8b5cf6; text-decoration: none; }
          </style>
        </head>
        <body>
          <div class="wrapper">
            <div class="card">
              <div class="logo"><span>Reprissa</span></div>
              CONTENT_PLACEHOLDER
            </div>
            <div class="footer">
              <p>&copy; 2024 Reprissa. Todos os direitos reservados.</p>
              <p><a href="https://reprissa.com.br">reprissa.com.br</a> &middot; <a href="https://instagram.com/reprissaoficial">@reprissaoficial</a></p>
            </div>
          </div>
        </body>
        </html>
        """.Replace("TITLE_PLACEHOLDER", title).Replace("CONTENT_PLACEHOLDER", content);

    /// <summary>
    /// Emite os ingressos individuais (um por unidade) de um pedido pago, cada um com código aleatório próprio.
    /// Idempotente: só cria o que falta (também cobre pedidos pagos antes do controle de entrada existir).
    /// </summary>
    private async Task EnsureIssuedAsync(TicketOrder order)
    {
        if (order.Quantity <= 0) return;

        var existing = await db.IssuedTickets.AsNoTracking()
            .Where(t => t.TicketOrderId == order.Id)
            .Select(t => t.Sequence)
            .ToListAsync();
        var missing = Enumerable.Range(1, order.Quantity).Where(n => !existing.Contains(n)).ToList();
        if (missing.Count == 0) return;

        foreach (var n in missing)
        {
            db.IssuedTickets.Add(new IssuedTicket
            {
                TicketOrderId = order.Id,
                TicketEventId = order.TicketEventId,
                EventTicketId = order.EventTicketId,
                Sequence = n,
                Code = TicketCodes.Generate(TicketCodes.TicketCodeLength)
            });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Corrida: outra requisição emitiu os mesmos ingressos primeiro — a próxima leitura já os encontra.
        }
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

        // Pedidos pagos: garante os ingressos individuais e carrega os códigos para o PDF.
        var paidOrders = rows.Where(r => r.Order.Status == TicketOrderStatus.Paid).Select(r => r.Order).ToList();
        foreach (var paidOrder in paidOrders)
            await EnsureIssuedAsync(paidOrder);
        var paidIds = paidOrders.Select(o => o.Id).ToList();
        var issued = paidIds.Count == 0
            ? new List<IssuedTicket>()
            : await db.IssuedTickets.AsNoTracking().Where(t => paidIds.Contains(t.TicketOrderId)).OrderBy(t => t.Sequence).ToListAsync();
        var issuedByOrder = issued
            .GroupBy(t => t.TicketOrderId)
            .ToDictionary(g => g.Key, g => g.Select(t => new IssuedTicketDto(t.Sequence, t.Code, t.CheckedInAt)).ToList());

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
                awaiting && !expired ? r.Order.CheckoutUrl : null,
                r.Event.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                r.Event.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                r.Event.Street,
                r.Event.Number,
                r.Event.Complement ?? string.Empty,
                r.Event.Neighborhood,
                r.Event.Cep,
                r.Event.ProducerName,
                r.Order.BuyerName,
                r.Order.BuyerEmail,
                r.Order.BuyerPhone,
                r.Order.BuyerCpf,
                issuedByOrder.TryGetValue(r.Order.Id, out var tickets) ? tickets : []);
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
