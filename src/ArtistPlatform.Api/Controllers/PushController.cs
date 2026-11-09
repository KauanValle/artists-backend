using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Inscrições de notificação push do PWA (Web Push / VAPID).</summary>
[ApiController]
[Route("api/push")]
public class PushController(IPushSubscriptionService subscriptions, IConfiguration configuration) : ControllerBase
{
    public record PushSubscriptionRequest(string Endpoint, KeysDto Keys);
    public record KeysDto(string P256dh, string Auth);
    public record RemoveSubscriptionRequest(string Endpoint);

    /// <summary>Chave pública VAPID para inscrição do navegador.</summary>
    [HttpGet("vapid-public-key")]
    [AllowAnonymous]
    public ActionResult<string> GetVapidPublicKey()
        => Ok(configuration["Vapid:PublicKey"] ?? string.Empty);

    /// <summary>Salva a inscrição push do dispositivo do usuário logado.</summary>
    [HttpPost("subscriptions")]
    [Authorize]
    public async Task<IActionResult> SaveSubscription(PushSubscriptionRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await subscriptions.SalvarAsync(userId, request.Endpoint, request.Keys.P256dh, request.Keys.Auth);
        return NoContent();
    }

    /// <summary>Remove a inscrição push (logout ou unsubscribe do navegador).</summary>
    [HttpPost("subscriptions/remover")]
    [Authorize]
    public async Task<IActionResult> RemoveSubscription(RemoveSubscriptionRequest request)
    {
        await subscriptions.RemoverAsync(request.Endpoint);
        return NoContent();
    }
}
