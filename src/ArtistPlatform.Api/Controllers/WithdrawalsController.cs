using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Services;
using ArtistPlatform.Domain.Enums;
using ArtistPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Saque do artista — shows concluídos com desconto da taxa administrativa.</summary>
[ApiController]
[Route("api/artist/withdrawals")]
[Authorize(Roles = "Artist")]
public class WithdrawalsController(
    IWithdrawalService withdrawalService,
    INotificationService notificationService,
    UserManager<AppUser> userManager) : ControllerBase
{
    /// <summary>Resumo de saque: shows concluídos, taxa e valor líquido.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<WithdrawalSummaryDto>> GetSummary()
        => Ok(await withdrawalService.GetArtistWithdrawalSummaryAsync());

    /// <summary>Lista minhas solicitações de saque.</summary>
    [HttpGet("my-requests")]
    public async Task<ActionResult<List<WithdrawalRequestDto>>> MyRequests()
        => Ok(await withdrawalService.ListMyRequestsAsync());

    /// <summary>Solicita saque — notifica admins.</summary>
    [HttpPost("request")]
    public async Task<ActionResult<CreateWithdrawalResultDto>> RequestWithdrawal()
    {
        var result = await withdrawalService.RequestWithdrawalAsync();

        // Notifica admins (precisa do UserManager que é da camada de Infrastructure)
        var admins = await userManager.Users
            .Where(u => u.Role == UserRole.Admin && !u.IsDeactivated)
            .Select(u => u.Id)
            .ToListAsync();

        var userName = User.Identity?.Name ?? "Artista";
        foreach (var adminId in admins)
        {
            await notificationService.NotifyAsync(adminId, NotificationType.WithdrawalRequested,
                "Nova solicitação de saque",
                $"Uma nova solicitação de saque foi enviada. Acesse o painel para revisar.",
                "/admin/saques");
        }

        return Ok(result);
    }
}

/// <summary>Painel admin de solicitações de saque.</summary>
[ApiController]
[Route("api/admin/withdrawals")]
[Authorize(Roles = "Admin")]
public class AdminWithdrawalsController(IWithdrawalService withdrawalService) : ControllerBase
{
    /// <summary>Lista todas as solicitações de saque.</summary>
    [HttpGet]
    public async Task<ActionResult<List<WithdrawalRequestDto>>> List()
        => Ok(await withdrawalService.ListAllRequestsAsync());

    /// <summary>Aprovar ou rejeitar uma solicitação de saque.</summary>
    [HttpPost("{id:guid}/process")]
    public async Task<ActionResult<WithdrawalRequestDto>> Process(Guid id, [FromBody] ProcessWithdrawalRequest request)
        => Ok(await withdrawalService.ProcessRequestAsync(id, request.Approve, request.Notes));
}

public record ProcessWithdrawalRequest(bool Approve, string? Notes);
