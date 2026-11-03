using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Taxa administrativa da plataforma — configurável pelo painel admin.</summary>
[ApiController]
[Route("api/admin/taxa")]
[Authorize(Roles = "Admin")]
public class AdminTaxaController(ITaxaAdministrativaService taxaService) : ControllerBase
{
    /// <summary>Percentual atual da taxa administrativa.</summary>
    [HttpGet]
    public async Task<ActionResult<TaxaAdministrativaDto>> Get()
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(new TaxaAdministrativaDto(await taxaService.GetPercentualAsync()));
    }

    /// <summary>Define o percentual da taxa (0–50%). Aplica-se aos próximos checkouts.</summary>
    [HttpPut]
    public async Task<ActionResult<TaxaAdministrativaDto>> Save(SaveTaxaAdministrativaRequest request)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(new TaxaAdministrativaDto(await taxaService.SetPercentualAsync(request.Percentual, adminId)));
    }
}
