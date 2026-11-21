using System.Collections.Concurrent;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Página pública de cobrança (/pagar/{id}): o id é um GUID não adivinhável.</summary>
[ApiController]
[Route("api/public/payment-links")]
[AllowAnonymous]
public class PublicPaymentLinksController(IAdminPaymentLinkService service) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, (DateTime WindowStart, int Count)> Attempts = new();
    private const int MaxPerMinute = 30;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicPaymentLinkDto>> Get(Guid id)
    {
        if (!TryConsume(ClientIp()))
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "TooManyRequests", title = "Muitas consultas. Aguarde um minuto e tente novamente.", status = 429 });

        var dto = await service.GetPublicAsync(id);
        return dto is null ? NotFound(new { error = "NotFound", title = "Cobrança não encontrada.", status = 404 }) : Ok(dto);
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
        return entry.Count <= MaxPerMinute;
    }
}
