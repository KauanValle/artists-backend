using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Api.Controllers;

public record AdminTicketEventDto(
    Guid Id,
    string Name,
    string ImageUrl,
    string Category,
    string StartDate,
    string StartTime,
    string EndDate,
    string EndTime,
    string Venue,
    string City,
    string State,
    string ProducerName,
    DateTime CreatedAt,
    string OwnerUserName,
    string OwnerUserEmail,
    int TotalTickets,
    int TotalSold,
    decimal GrossRevenue);

public record AdminTestTicketDto(
    Guid OrderId,
    Guid IssuedTicketId,
    string Code,
    string TicketTitle,
    string BuyerName);

public record CreateTestTicketRequest(Guid EventTicketId);

/// <summary>Painel admin: visão de todos os eventos de ingressos da plataforma.</summary>
[ApiController]
[Route("api/admin/ticket-events")]
[Authorize(Roles = "Admin")]
public class AdminTicketEventsController(
    IAppDbContext db,
    UserManager<AppUser> userManager,
    ITicketEventService ticketEventService,
    ITicketManagementService managementService) : ControllerBase
{
    /// <summary>Lista todos os eventos de ingressos da plataforma com dados do dono.</summary>
    [HttpGet]
    public async Task<ActionResult<List<AdminTicketEventDto>>> ListAll()
    {
        var events = await db.TicketEvents.AsNoTracking()
            .Include(e => e.Tickets)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var ownerIds = events.Select(e => e.OwnerUserId).Distinct().ToList();
        var owners = await userManager.Users.AsNoTracking()
            .Where(u => ownerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToDictionaryAsync(u => u.Id);

        return Ok(events.Select(e =>
        {
            var owner = owners.GetValueOrDefault(e.OwnerUserId);
            return new AdminTicketEventDto(
                e.Id,
                e.Name,
                e.ImageUrl,
                e.Category,
                e.StartDate.ToString("yyyy-MM-dd"),
                e.StartTime.ToString("HH:mm"),
                e.EndDate.ToString("yyyy-MM-dd"),
                e.EndTime.ToString("HH:mm"),
                e.VenueName,
                e.City,
                e.State,
                e.ProducerName,
                e.CreatedAt,
                owner?.DisplayName ?? "—",
                owner?.Email ?? "—",
                e.Tickets.Sum(t => t.Quantity),
                e.Tickets.Sum(t => t.SoldQuantity),
                e.Tickets.Sum(t => t.SoldQuantity * t.BuyerPrice));
        }).ToList());
    }

    /// <summary>Admin visualiza qualquer evento (usa a rota pública que não verifica dono).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketEventDto>> Get(Guid id)
        => Ok(await ticketEventService.GetPublicAsync(id));

    /// <summary>Admin acessa stats de qualquer evento (sem verificação de dono).</summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<TicketEventStatsDto>> Stats(Guid id)
        => Ok(await managementService.GetStatsAdminAsync(id));

    /// <summary>Admin acessa pedidos de qualquer evento.</summary>
    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<List<EventOrderDto>>> Orders(Guid id)
        => Ok(await managementService.ListOrdersAdminAsync(id));

    /// <summary>Admin acessa check-ins de qualquer evento.</summary>
    [HttpGet("{id:guid}/check-ins")]
    public async Task<ActionResult<List<CheckInLogDto>>> CheckIns(Guid id)
        => Ok(await managementService.ListCheckInsAdminAsync(id));

    /// <summary>Admin acessa portarias de qualquer evento.</summary>
    [HttpGet("{id:guid}/gates")]
    public async Task<ActionResult<List<GateAccessDto>>> Gates(Guid id)
        => Ok(await managementService.ListGatesAdminAsync(id));

    /// <summary>Gera um ingresso de teste (sem pagamento, sem validade) para fins de visualização e teste de QR code.</summary>
    [HttpPost("{eventId:guid}/test-ticket")]
    public async Task<ActionResult<AdminTestTicketDto>> CreateTestTicket(Guid eventId, [FromBody] CreateTestTicketRequest request)
    {
        var ev = await db.TicketEvents.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new NotFoundException("Evento não encontrado.");

        var ticket = await db.EventTickets
            .FirstOrDefaultAsync(t => t.Id == request.EventTicketId && t.TicketEventId == eventId)
            ?? throw new NotFoundException("Tipo de ingresso não encontrado.");

        // Cria um pedido de teste (pago direto, sem checkout)
        var order = new TicketOrder
        {
            TicketEventId = eventId,
            EventTicketId = request.EventTicketId,
            BuyerName = "TESTE ADMIN",
            BuyerPhone = "00000000000",
            BuyerEmail = "teste@admin.local",
            BuyerCpf = "00000000000",
            Quantity = 1,
            UnitPrice = 0,
            TotalAmount = 0,
            Status = TicketOrderStatus.Paid,
            PaidAt = DateTime.UtcNow
        };
        db.TicketOrders.Add(order);

        // Emite o ingresso individual
        var issued = new IssuedTicket
        {
            TicketOrderId = order.Id,
            TicketEventId = eventId,
            EventTicketId = request.EventTicketId,
            Sequence = 1,
            Code = TicketCodes.Generate(TicketCodes.TicketCodeLength)
        };
        db.IssuedTickets.Add(issued);

        // NÃO incrementa SoldQuantity — é um ingresso de teste.

        await db.SaveChangesAsync();

        return Ok(new AdminTestTicketDto(
            order.Id,
            issued.Id,
            issued.Code,
            ticket.Title,
            "TESTE ADMIN"));
    }

    public record AdminOrderDetailDto(
        Guid Id,
        string BuyerName,
        string BuyerEmail,
        string BuyerPhone,
        string BuyerCpf,
        string TicketTitle,
        int Quantity,
        decimal UnitPrice,
        decimal TotalAmount,
        string Status,
        bool Expired,
        DateTime CreatedAt,
        DateTime? PaidAt,
        string EventName,
        string EventImageUrl,
        string EventStartDate,
        string EventStartTime,
        string EventEndDate,
        string EventEndTime,
        string Venue,
        string City,
        string State,
        string Street,
        string Number,
        string Complement,
        string Neighborhood,
        string Cep,
        string ProducerName,
        List<AdminIssuedTicketDto> Tickets);

    public record AdminIssuedTicketDto(Guid Id, int Sequence, string Code, DateTime? CheckedInAt, string? CheckedInBy);

    /// <summary>Detalhe completo de um pedido com ingressos emitidos (códigos QR).</summary>
    [HttpGet("{eventId:guid}/orders/{orderId:guid}/detail")]
    public async Task<ActionResult<AdminOrderDetailDto>> OrderDetail(Guid eventId, Guid orderId)
    {
        var row = await (
            from o in db.TicketOrders.AsNoTracking()
            join t in db.EventTickets.AsNoTracking() on o.EventTicketId equals t.Id
            join e in db.TicketEvents.AsNoTracking() on o.TicketEventId equals e.Id
            where o.Id == orderId && o.TicketEventId == eventId
            select new { Order = o, TicketTitle = t.Title, Event = e }).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Pedido não encontrado.");

        var tickets = await db.IssuedTickets.AsNoTracking()
            .Where(i => i.TicketOrderId == orderId)
            .OrderBy(i => i.Sequence)
            .Select(i => new AdminIssuedTicketDto(i.Id, i.Sequence, i.Code, i.CheckedInAt, i.CheckedInBy))
            .ToListAsync();

        var limit = DateTime.UtcNow - TicketOrderService.ReservationHold;
        var awaiting = row.Order.Status == TicketOrderStatus.AwaitingPayment;

        return Ok(new AdminOrderDetailDto(
            row.Order.Id,
            row.Order.BuyerName,
            row.Order.BuyerEmail,
            row.Order.BuyerPhone,
            row.Order.BuyerCpf,
            row.TicketTitle,
            row.Order.Quantity,
            row.Order.UnitPrice,
            row.Order.TotalAmount,
            row.Order.Status,
            awaiting && row.Order.CreatedAt <= limit,
            row.Order.CreatedAt,
            row.Order.PaidAt,
            row.Event.Name,
            row.Event.ImageUrl,
            row.Event.StartDate.ToString("yyyy-MM-dd"),
            row.Event.StartTime.ToString("HH:mm"),
            row.Event.EndDate.ToString("yyyy-MM-dd"),
            row.Event.EndTime.ToString("HH:mm"),
            row.Event.VenueName,
            row.Event.City,
            row.Event.State,
            row.Event.Street,
            row.Event.Number,
            row.Event.Complement ?? "",
            row.Event.Neighborhood,
            row.Event.Cep,
            row.Event.ProducerName,
            tickets));
    }

    /// <summary>
    /// Admin contabiliza (força pagamento) de um pedido expirado.
    /// Muda o status para Paid, incrementa SoldQuantity e emite os ingressos.
    /// </summary>
    [HttpPost("{eventId:guid}/orders/{orderId:guid}/force-pay")]
    public async Task<IActionResult> ForcePay(Guid eventId, Guid orderId)
    {
        var order = await db.TicketOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.TicketEventId == eventId)
            ?? throw new NotFoundException("Pedido não encontrado.");

        if (order.Status == TicketOrderStatus.Paid)
            return Ok(new { message = "Pedido já está pago." });

        if (order.Status == TicketOrderStatus.Cancelled)
            throw new BusinessRuleException("Pedido cancelado não pode ser contabilizado.");

        var now = DateTime.UtcNow;
        // Marca como pago
        order.Status = TicketOrderStatus.Paid;
        order.PaidAt = now;
        order.UpdatedAt = now;

        // Incrementa SoldQuantity
        await db.EventTickets
            .Where(t => t.Id == order.EventTicketId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SoldQuantity, t => t.SoldQuantity + order.Quantity));

        // Emite ingressos individuais
        var existing = await db.IssuedTickets.AsNoTracking()
            .Where(t => t.TicketOrderId == order.Id)
            .Select(t => t.Sequence)
            .ToListAsync();
        var missing = Enumerable.Range(1, order.Quantity).Where(n => !existing.Contains(n)).ToList();
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

        await db.SaveChangesAsync();

        return Ok(new { message = "Pedido contabilizado com sucesso.", ticketsIssued = missing.Count });
    }

    /// <summary>
    /// Admin descontabiliza um pedido pago — reverte para AwaitingPayment,
    /// decrementa SoldQuantity e remove os ingressos emitidos.
    /// </summary>
    [HttpPost("{eventId:guid}/orders/{orderId:guid}/undo-pay")]
    public async Task<IActionResult> UndoPay(Guid eventId, Guid orderId)
    {
        var order = await db.TicketOrders.FirstOrDefaultAsync(o => o.Id == orderId && o.TicketEventId == eventId)
            ?? throw new NotFoundException("Pedido não encontrado.");

        if (order.Status != TicketOrderStatus.Paid)
            throw new BusinessRuleException("Apenas pedidos pagos podem ser descontabilizados.");

        var now = DateTime.UtcNow;
        // Reverte para AwaitingPayment
        order.Status = TicketOrderStatus.AwaitingPayment;
        order.PaidAt = null;
        order.UpdatedAt = now;

        // Decrementa SoldQuantity
        await db.EventTickets
            .Where(t => t.Id == order.EventTicketId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SoldQuantity, t => t.SoldQuantity - order.Quantity));

        // Remove ingressos emitidos deste pedido
        await db.IssuedTickets
            .Where(i => i.TicketOrderId == order.Id)
            .ExecuteDeleteAsync();

        await db.SaveChangesAsync();

        return Ok(new { message = "Pedido descontabilizado com sucesso.", ticketsRemoved = order.Quantity });
    }
}
