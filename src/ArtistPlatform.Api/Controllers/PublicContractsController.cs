using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>
/// Endpoints públicos do contrato via token de capacidade (link de assinatura do contratante).
/// O token é gerado quando o artista assina; a assinatura do contratante registra as mesmas
/// evidências (hash, data/hora UTC com segundos, IP e user-agent).
/// </summary>
[ApiController]
[Route("api/public/contracts")]
public class PublicContractsController(IContractService contractService) : ControllerBase
{
    /// <summary>Visualização pública do contrato pelo token (não exige cadastro).</summary>
    [HttpGet("{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtistContractDto>> GetByToken(string token)
        => Ok(await contractService.GetByTokenAsync(token));

    /// <summary>Assinatura eletrônica do contratante pelo token.</summary>
    [HttpPost("{token}/sign")]
    [AllowAnonymous]
    public async Task<ActionResult<ArtistContractDto>> SignByContractor(string token, SignContractorRequest request)
    {
        var ip = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                 ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                 ?? "desconhecido";
        var userAgent = Request.Headers.UserAgent.ToString();

        return Ok(await contractService.SignByContractorAsync(token, request, ip, userAgent));
    }
}
