using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json;

namespace ArtistPlatform.Api.Middleware;

/// <summary>
/// Gate de assinatura: contas de artista no cargo Gratuito só acessam auth, billing, webhooks,
/// notificações e a leitura do próprio perfil (GET /api/artists/me) — todo o restante responde
/// 403 até o webhook do pagamento promover a conta para Assinante. Rotas [AllowAnonymous]
/// (marketplace público etc.) continuam abertas. Contratante e Admin não são afetados.
/// </summary>
public class SubscriptionGateMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPathPrefixes =
        ["/api/auth", "/api/billing", "/api/webhooks", "/api/notifications"];

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        if (IsBlockedFreeArtist(context, out var userId))
        {
            var tier = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId!.Value)
                .Select(u => u.SubscriptionTier)
                .FirstOrDefaultAsync();

            if (tier != SubscriptionTier.Assinante)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "Assinatura necessária: sua conta é gratuita. Assine o plano para liberar todas as funções.",
                    title = "Assinatura necessária",
                    status = 403
                }));
                return;
            }
        }

        await next(context);
    }

    private static bool IsBlockedFreeArtist(HttpContext context, out Guid? userId)
    {
        userId = null;
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, UserRole.Artist.ToString(), StringComparison.OrdinalIgnoreCase))
            return false;

        // Rotas públicas ([AllowAnonymous]) seguem abertas mesmo para contas gratuitas.
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // O artista gratuito precisa enxergar o próprio perfil (menu/alertas), mas não editá-lo.
        if (path.Equals("/api/artists/me", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
            return false;

        userId = Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        return userId.HasValue;
    }
}
