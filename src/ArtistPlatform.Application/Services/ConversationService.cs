using ArtistPlatform.Application.Common;
using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Interfaces;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Services;

public interface IConversationService
{
    Task<List<ConversationDto>> ListForUserAsync();
    Task<(ConversationDto Conversation, List<MessageDto> Messages)> GetAsync(Guid id);
    Task<MessageDto> SendMessageAsync(Guid id, SendMessageRequestDto request);
    Task MarkReadAsync(Guid id);
}

public class ConversationService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    IUserDirectory userDirectory,
    INotificationService notifications) : IConversationService
{
    public async Task<List<ConversationDto>> ListForUserAsync()
    {
        var userId = currentUser.EnsureAuthenticated();
        var conversations = await db.Conversations.AsNoTracking()
            .Where(c => c.ArtistUserId == userId || c.ContractorUserId == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

        var otherIds = conversations.Select(c => c.ArtistUserId == userId ? c.ContractorUserId : c.ArtistUserId).ToList();
        var users = await userDirectory.GetUsersAsync(otherIds);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var allMessages = await db.Messages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .ToListAsync();

        var lastMessages = allMessages
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First());
        var unread = allMessages
            .Where(m => !m.IsRead && m.SenderUserId != userId)
            .GroupBy(m => m.ConversationId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<ConversationDto>();
        foreach (var c in conversations)
        {
            var otherId = c.ArtistUserId == userId ? c.ContractorUserId : c.ArtistUserId;
            var info = users.GetValueOrDefault(otherId);
            var last = lastMessages.GetValueOrDefault(c.Id);
            var unreadCount = unread.GetValueOrDefault(c.Id);
            result.Add(new ConversationDto(
                c.Id, c.BookingRequestId, otherId, info?.DisplayName ?? "Usuário", info?.PhotoUrl,
                last?.Content, last?.CreatedAt, unreadCount));
        }
        return result;
    }

    public async Task<(ConversationDto Conversation, List<MessageDto> Messages)> GetAsync(Guid id)
    {
        var conversation = await EnsureParticipantAsync(id);

        var messages = await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var senderIds = messages.Select(m => m.SenderUserId).Append(conversation.ArtistUserId).Append(conversation.ContractorUserId).Distinct().ToList();
        var users = await userDirectory.GetUsersAsync(senderIds);
        var otherId = conversation.ArtistUserId == currentUser.UserId ? conversation.ContractorUserId : conversation.ArtistUserId;
        var otherInfo = users.GetValueOrDefault(otherId);

        var dto = new ConversationDto(id, conversation.BookingRequestId, otherId,
            otherInfo?.DisplayName ?? "Usuário", otherInfo?.PhotoUrl, null, null, 0);

        return (dto, messages.Select(m => new MessageDto(
            m.Id, m.SenderUserId, users.GetValueOrDefault(m.SenderUserId)?.DisplayName ?? "Usuário",
            m.Content, m.CreatedAt, m.IsRead)).ToList());
    }

    public async Task<MessageDto> SendMessageAsync(Guid id, SendMessageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new ValidationException("A mensagem não pode estar vazia.");

        var conversation = await EnsureParticipantAsync(id);
        var userId = currentUser.UserId!.Value;

        var message = new Message
        {
            ConversationId = id,
            SenderUserId = userId,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Messages.Add(message);

        conversation.LastMessageAt = message.CreatedAt;
        conversation.UpdatedAt = message.CreatedAt;
        await db.SaveChangesAsync();

        var otherId = conversation.ArtistUserId == userId ? conversation.ContractorUserId : conversation.ArtistUserId;
        var senderName = (await userDirectory.GetUsersAsync([userId])).GetValueOrDefault(userId)?.DisplayName ?? "Usuário";
        await notifications.NotifyAsync(otherId, NotificationType.NewMessage,
            "Nova mensagem",
            $"{senderName}: {message.Content[..Math.Min(80, message.Content.Length)]}",
            $"/conversas/{id}");

        return new MessageDto(message.Id, userId, senderName, message.Content, message.CreatedAt, false);
    }

    /// <summary>Marca estado de leitura das mensagens recebidas (PRD §19).</summary>
    public async Task MarkReadAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var unread = await db.Messages
            .Where(m => m.ConversationId == id && !m.IsRead && m.SenderUserId != userId)
            .ToListAsync();
        foreach (var m in unread)
        {
            m.IsRead = true;
            m.ReadAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private async Task<Conversation> EnsureParticipantAsync(Guid id)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id)
            ?? throw new NotFoundException("Conversa não encontrada.");
        var userId = currentUser.EnsureAuthenticated();
        if (conversation.ArtistUserId != userId && conversation.ContractorUserId != userId)
            throw new ForbiddenException();
        return conversation;
    }
}
