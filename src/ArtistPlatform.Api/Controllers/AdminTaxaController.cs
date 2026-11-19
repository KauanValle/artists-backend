using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Taxa administrativa da plataforma — configurável pelo painel admin.</summary>
[ApiController]
[Route("api/admin/taxa")]
[Authorize(Roles = "Admin")]
public class AdminTaxaController(ITaxaAdministrativaService taxaService, IAppDbContext db) : ControllerBase
{
    /// <summary>Percentual atual da taxa administrativa (shows).</summary>
    [HttpGet]
    public async Task<ActionResult<TaxaAdministrativaDto>> Get()
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(new TaxaAdministrativaDto(await taxaService.GetPercentualAsync()));
    }

    /// <summary>Define o percentual da taxa de shows (0–50%). Aplica-se aos próximos checkouts.</summary>
    [HttpPut]
    public async Task<ActionResult<TaxaAdministrativaDto>> Save(SaveTaxaAdministrativaRequest request)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(new TaxaAdministrativaDto(await taxaService.SetPercentualAsync(request.Percentual, adminId)));
    }

    // ── Taxa administrativa de ingressos ──

    /// <summary>Percentual atual da taxa administrativa de ingressos.</summary>
    [HttpGet("ingressos")]
    public async Task<ActionResult<TaxaAdministrativaDto>> GetTaxaIngressos()
    {
        var value = await db.Settings.AsNoTracking()
            .Where(s => s.Key == TicketEventService.FeeSettingsKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var percentual = decimal.TryParse(value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var fee)
            ? Math.Clamp(fee, 0m, 50m)
            : TicketEventService.DefaultFeePercent;

        return Ok(new TaxaAdministrativaDto(percentual));
    }

    /// <summary>Define o percentual da taxa de ingressos (0–50%). Aplica-se aos próximos cadastros de ingressos.</summary>
    [HttpPut("ingressos")]
    public async Task<ActionResult<TaxaAdministrativaDto>> SaveTaxaIngressos(SaveTaxaAdministrativaRequest request)
    {
        if (request.Percentual < 0 || request.Percentual > 50)
            return BadRequest(new { error = "A taxa deve estar entre 0% e 50%." });

        var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == TicketEventService.FeeSettingsKey);
        if (setting is null)
        {
            setting = new AppSetting { Key = TicketEventService.FeeSettingsKey, Value = request.Percentual.ToString("0.##") };
            db.Settings.Add(setting);
        }
        else
        {
            setting.Value = request.Percentual.ToString("0.##");
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return Ok(new TaxaAdministrativaDto(request.Percentual));
    }
}
