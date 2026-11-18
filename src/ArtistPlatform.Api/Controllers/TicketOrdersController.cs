using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Compra pública de ingressos (sem login).</summary>
[ApiController]
[Route("api/public/ticket-events/{eventId:guid}/orders")]
[AllowAnonymous]
public class PublicTicketOrdersController(ITicketOrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketOrderDto>> Create(Guid eventId, CreateTicketOrderRequestDto request)
        => Ok(await orderService.CreateAsync(eventId, request));

    /// <summary>Cria o checkout do InfinityPay; o valor é sempre lido do banco.</summary>
    [HttpPost("{orderId:guid}/checkout")]
    public async Task<ActionResult<TicketCheckoutDto>> Checkout(Guid eventId, Guid orderId)
        => Ok(await orderService.StartCheckoutAsync(eventId, orderId));
}

public record LookupTicketOrdersRequestDto(string Cpf, string Phone);

public record VerifyTicketReturnRequestDto(string OrderNsu, string? TransactionNsu, string? Slug);

/// <summary>Consulta pública "acompanhar pedidos": CPF + telefone do comprador.</summary>
[ApiController]
[Route("api/public/ticket-orders")]
[AllowAnonymous]
public class PublicTicketOrderLookupController(ITicketOrderService orderService) : ControllerBase
{
    // Limite simples por IP (10 consultas/minuto) para dificultar tentativa-e-erro de CPF/telefone.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime WindowStart, int Count)> Attempts = new();
    private const int MaxAttemptsPerMinute = 10;

    [HttpPost("lookup")]
    public async Task<ActionResult<List<TicketOrderSummaryDto>>> Lookup(LookupTicketOrdersRequestDto request)
    {
        if (!TryConsume(ClientIp()))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "TooManyRequests", title = "Muitas consultas. Aguarde um minuto e tente novamente.", status = 429 });

        return Ok(await orderService.LookupAsync(request.Cpf, request.Phone));
    }

    /// <summary>Chamado pela página de retorno do checkout: confere o pagamento direto no InfinityPay.</summary>
    [HttpPost("verify")]
    public async Task<ActionResult<TicketReturnDto>> Verify(VerifyTicketReturnRequestDto request)
    {
        if (!TryConsume(ClientIp()))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "TooManyRequests", title = "Muitas consultas. Aguarde um minuto e tente novamente.", status = 429 });

        return Ok(await orderService.VerifyReturnAsync(request.OrderNsu, request.TransactionNsu, request.Slug));
    }

    private string ClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded)) return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool TryConsume(string ip)
    {
        var now = DateTime.UtcNow;
        var entry = Attempts.AddOrUpdate(ip,
            _ => (now, 1),
            (_, cur) => now - cur.WindowStart > TimeSpan.FromMinutes(1) ? (now, 1) : (cur.WindowStart, cur.Count + 1));
        if (Attempts.Count > 5000)
            foreach (var kv in Attempts.Where(k => now - k.Value.WindowStart > TimeSpan.FromMinutes(1)).ToList())
                Attempts.TryRemove(kv.Key, out _);
        return entry.Count <= MaxAttemptsPerMinute;
    }
}
