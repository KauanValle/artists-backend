using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Links de pagamento avulsos (InfinityPay) gerados pelo admin.</summary>
[ApiController]
[Route("api/admin/payment-links")]
[Authorize(Roles = "Admin")]
public class AdminPaymentLinksController(IAdminPaymentLinkService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdminPaymentLinkDto>>> List() => Ok(await service.ListAsync());

    [HttpPost]
    public async Task<ActionResult<AdminPaymentLinkDto>> Create(CreateAdminPaymentLinkRequestDto request)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        return Ok(await service.CreateAsync(adminId, request));
    }

    [HttpPost("{id:guid}/refresh")]
    public async Task<ActionResult<AdminPaymentLinkDto>> Refresh(Guid id) => Ok(await service.RefreshAsync(id));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<AdminPaymentLinkDto>> Cancel(Guid id) => Ok(await service.CancelAsync(id));
}
