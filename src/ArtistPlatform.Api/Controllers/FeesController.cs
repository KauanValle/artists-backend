using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/artists/{artistId:guid}/fees")]
public class FeesController(IFeeService feeService) : ControllerBase
{
    /// <summary>Faixas de cachê públicas (PRD §16): "a partir de R$ X".</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<FeeDto>>> ListByArtist(Guid artistId)
        => Ok(await feeService.ListByArtistAsync(artistId));
}

[ApiController]
[Route("api/artists/me/fees")]
[Authorize(Roles = "Artist")]
public class MyFeesController(IFeeService feeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<FeeDto>>> ListMine()
        => Ok(await feeService.ListMineAsync());

    /// <summary>Cadastra faixa de cachê (RN004: múltiplas faixas; RN005: valor é referência).</summary>
    [HttpPost]
    public async Task<ActionResult<FeeDto>> Create(SaveFeeRequest request)
        => Ok(await feeService.CreateAsync(request));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FeeDto>> Update(Guid id, SaveFeeRequest request)
        => Ok(await feeService.UpdateAsync(id, request));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await feeService.DeleteAsync(id);
        return NoContent();
    }
}
