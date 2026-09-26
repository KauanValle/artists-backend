using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtistPlatform.Api.Controllers;

/// <summary>Chat: uma conversa por contratação (PRD §19).</summary>
[ApiController]
[Route("api/conversations")]
[Authorize]
public class ConversationsController(IConversationService conversationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ConversationDto>>> List()
        => Ok(await conversationService.ListForUserAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDetailResponse>> Get(Guid id)
    {
        var (conversation, messages) = await conversationService.GetAsync(id);
        await conversationService.MarkReadAsync(id);
        return Ok(new ConversationDetailResponse(conversation, messages));
    }

    public record ConversationDetailResponse(ConversationDto Conversation, List<MessageDto> Messages);

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid id, SendMessageRequestDto request)
        => Ok(await conversationService.SendMessageAsync(id, request));

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await conversationService.MarkReadAsync(id);
        return NoContent();
    }
}
