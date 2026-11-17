using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/artist/referrals")]
[Authorize(Roles = "Artist")]
public class ReferralsController(IReferralService referralService) : ControllerBase
{
    /// <summary>Resumo de indicações do artista logado: link, código e lista de indicados.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ReferralSummaryDto>> GetSummary()
    {
        // Monta a URL base do frontend a partir do header Origin ou Referer
        var origin = Request.Headers.Origin.FirstOrDefault()
            ?? Request.Headers.Referer.FirstOrDefault();

        if (!string.IsNullOrEmpty(origin))
        {
            var uri = new Uri(origin);
            origin = $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}";
        }
        else
        {
            origin = $"{Request.Scheme}://{Request.Host}";
        }

        return Ok(await referralService.GetArtistReferralSummaryAsync(origin));
    }
}
