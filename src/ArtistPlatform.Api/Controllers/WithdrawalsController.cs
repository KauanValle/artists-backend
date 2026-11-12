using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Saque do artista — shows concluídos com desconto da taxa administrativa.</summary>
[ApiController]
[Route("api/artist/withdrawals")]
[Authorize(Roles = "Artist")]
public class WithdrawalsController(IWithdrawalService withdrawalService) : ControllerBase
{
    /// <summary>
    /// Retorna o resumo de saque do artista: shows concluídos com pagamento confirmado,
    /// taxa administrativa descontada e valor líquido disponível.
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<WithdrawalSummaryDto>> GetSummary()
        => Ok(await withdrawalService.GetArtistWithdrawalSummaryAsync());
}
