using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

[ApiController]
[Route("api/proposals")]
[Authorize]
public class ProposalsController(IProposalService proposalService) : ControllerBase
{
    /// <summary>Artista envia proposta formal (itens + deslocamento + equipamento − desconto — PRD §20).</summary>
    [HttpPost]
    [Authorize(Roles = "Artist")]
    public async Task<ActionResult<ProposalDto>> Create(CreateProposalRequestDto request)
        => Ok(await proposalService.CreateAsync(request));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProposalDto>> Get(Guid id)
        => Ok(await proposalService.GetAsync(id));

    [HttpGet("booking/{bookingId:guid}")]
    public async Task<ActionResult<List<ProposalDto>>> ListForBooking(Guid bookingId)
        => Ok(await proposalService.ListForBookingAsync(bookingId));

    /// <summary>
    /// Contratante aceita: proposta ACCEPTED, evento CONFIRMED criado, agenda bloqueada,
    /// receita PENDING criada e notificações (PRD §11; RN007/RN008/RN010; RN017/RN019).
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = "Contractor")]
    public async Task<ActionResult<ProposalDto>> Accept(Guid id)
        => Ok(await proposalService.AcceptAsync(id));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Contractor")]
    public async Task<ActionResult<ProposalDto>> Reject(Guid id)
        => Ok(await proposalService.RejectAsync(id));
}
