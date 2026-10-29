using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Api.Controllers;

public record BrandingDto(string? LogoUrl);

/// <summary>
/// Identidade visual da plataforma: logo personalizada enviada pelo admin (armazenada no
/// storage via /api/uploads) e servida publicamente para toda a interface.
/// </summary>
[ApiController]
[Route("api/branding")]
public class BrandingController(IAppDbContext db, ICurrentUserService currentUser, IFileStorage fileStorage) : ControllerBase
{
    private const string LogoKey = "branding:logo";

    /// <summary>Logo personalizada da plataforma (null = usa o símbolo padrão).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<BrandingDto>> Get()
    {
        var logoUrl = await db.Settings.AsNoTracking()
            .Where(s => s.Key == LogoKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        return Ok(new BrandingDto(logoUrl));
    }

    /// <summary>Envia uma nova logo (imagem) — somente admin.</summary>
    [HttpPost("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BrandingDto>> UploadLogo(IFormFile file)
    {
        currentUser.EnsureAuthenticated();
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas administradores podem alterar a identidade visual.");

        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Arquivo vazio." });

        var allowed = new[] { "image/png", "image/jpeg", "image/webp", "image/svg+xml" };
        if (!allowed.Contains(file.ContentType))
            return BadRequest(new { error = "Formato não suportado. Use PNG, JPG, WEBP ou SVG." });
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest(new { error = "Imagem muito grande. Limite de 2 MB." });

        var adminUserId = currentUser.UserId!.Value;
        await using var stream = file.OpenReadStream();
        var url = await fileStorage.SaveAsync(adminUserId, stream, file.FileName, file.ContentType);

        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == LogoKey);
        if (setting is null)
        {
            setting = new AppSetting { Key = LogoKey };
            db.Settings.Add(setting);
        }
        setting.Value = url;
        await db.SaveChangesAsync();

        return Ok(new BrandingDto(url));
    }

    /// <summary>Remove a logo personalizada (volta ao símbolo padrão).</summary>
    [HttpDelete("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveLogo()
    {
        currentUser.EnsureAuthenticated();
        if (currentUser.Role != UserRole.Admin)
            throw new ForbiddenException("Apenas administradores podem alterar a identidade visual.");

        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == LogoKey);
        if (setting is not null)
        {
            if (!string.IsNullOrWhiteSpace(setting.Value))
            {
                fileStorage.Delete(setting.Value);
            }

            db.Settings.Remove(setting);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}
