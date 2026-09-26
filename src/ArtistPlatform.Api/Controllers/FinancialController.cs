using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Financeiro (PRD §12): receitas e despesas com status de pagamento (RN011/RN012).</summary>
[ApiController]
[Route("api/financial")]
[Authorize(Roles = "Artist")]
public class FinancialController(IFinancialService financialService) : ControllerBase
{
    [HttpGet("transactions")]
    public async Task<ActionResult<PagedResult<TransactionDto>>> List(
        [FromQuery] TransactionType? type, [FromQuery] TransactionStatus? status,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await financialService.ListAsync(type, status, from, to, page, pageSize));

    [HttpPost("transactions")]
    public async Task<ActionResult<TransactionDto>> Create(CreateTransactionRequestDto request)
        => Ok(await financialService.CreateAsync(request));

    [HttpPut("transactions/{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, UpdateTransactionRequestDto request)
        => Ok(await financialService.UpdateAsync(id, request));

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await financialService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Marca receita como recebida / despesa como paga.</summary>
    [HttpPost("transactions/{id:guid}/settle")]
    public async Task<ActionResult<TransactionDto>> Settle(Guid id)
        => Ok(await financialService.MarkSettledAsync(id));

    /// <summary>Dashboard: previsto × recebido, despesas, resultado e contas a receber (PRD §7/§12).</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<FinancialSummaryDto>> Summary(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await financialService.GetSummaryAsync(from, to));
}
