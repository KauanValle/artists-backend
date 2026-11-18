using System.Collections.Concurrent;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

public record CheckInRequestDto(string Code);
public record CreateGateRequestDto(string Name);

/// <summary>Painel do produtor: estatísticas, pedidos, check-in e acessos de portaria do próprio evento.</summary>
[ApiController]
[Route("api/ticket-events/{eventId:guid}")]
[Authorize]
public class TicketManagementController(ITicketManagementService management) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<TicketEventStatsDto>> Stats(Guid eventId)
        => Ok(await management.GetStatsAsync(eventId));

    [HttpGet("orders")]
    public async Task<ActionResult<List<EventOrderDto>>> Orders(Guid eventId)
        => Ok(await management.ListOrdersAsync(eventId));

    /// <summary>Leitura do QR code pelo próprio produtor (sem precisar de código de portaria).</summary>
    [HttpPost("check-in")]
    public async Task<ActionResult<CheckInResultDto>> CheckIn(Guid eventId, CheckInRequestDto request)
        => Ok(await management.CheckInAsOwnerAsync(eventId, request.Code));

    [HttpGet("check-ins")]
    public async Task<ActionResult<List<CheckInLogDto>>> CheckIns(Guid eventId)
        => Ok(await management.ListCheckInsAsync(eventId));

    [HttpPost("check-ins/{ticketId:guid}/undo")]
    public async Task<IActionResult> Undo(Guid eventId, Guid ticketId)
    {
        await management.UndoCheckInAsync(eventId, ticketId);
        return NoContent();
    }

    [HttpGet("gates")]
    public async Task<ActionResult<List<GateAccessDto>>> Gates(Guid eventId)
        => Ok(await management.ListGatesAsync(eventId));

    [HttpPost("gates")]
    public async Task<ActionResult<GateAccessDto>> CreateGate(Guid eventId, CreateGateRequestDto request)
        => Ok(await management.CreateGateAsync(eventId, request.Name));

    [HttpDelete("gates/{gateId:guid}")]
    public async Task<IActionResult> RevokeGate(Guid eventId, Guid gateId)
    {
        await management.RevokeGateAsync(eventId, gateId);
        return NoContent();
    }
}

public record GateOpenRequestDto(string Code);
public record GateCheckInRequestDto(string GateCode, string TicketCode);

/// <summary>Portaria: leitura de QR codes sem login, identificada pelo código do acesso criado pelo produtor.</summary>
[ApiController]
[Route("api/public/gate")]
[AllowAnonymous]
public class PublicGateController(ITicketManagementService management) : ControllerBase
{
    // Limite por IP (240 requisições/minuto) — suficiente para uma fila de leitura, bloqueia força bruta do código.
    private static readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> Attempts = new();
    private const int MaxPerMinute = 240;

    [HttpPost("session")]
    public async Task<ActionResult<GateSessionDto>> Open(GateOpenRequestDto request)
    {
        if (!TryConsume()) return TooMany();
        return Ok(await management.OpenGateAsync(request.Code));
    }

    [HttpPost("check-in")]
    public async Task<ActionResult<CheckInResultDto>> CheckIn(GateCheckInRequestDto request)
    {
        if (!TryConsume()) return TooMany();
        return Ok(await management.CheckInAtGateAsync(request.GateCode, request.TicketCode));
    }

    private ObjectResult TooMany() => StatusCode(StatusCodes.Status429TooManyRequests,
        new { error = "TooManyRequests", title = "Muitas requisições. Aguarde um instante e tente novamente.", status = 429 });

    private bool TryConsume()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = !string.IsNullOrWhiteSpace(forwarded)
            ? forwarded.Split(',')[0].Trim()
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var now = DateTime.UtcNow;
        var entry = Attempts.AddOrUpdate(ip,
            _ => (now, 1),
            (_, cur) => now - cur.WindowStart > TimeSpan.FromMinutes(1) ? (now, 1) : (cur.WindowStart, cur.Count + 1));
        if (Attempts.Count > 5000)
            foreach (var kv in Attempts.Where(k => now - k.Value.WindowStart > TimeSpan.FromMinutes(1)).ToList())
                Attempts.TryRemove(kv.Key, out _);
        return entry.Count <= MaxPerMinute;
    }
}
