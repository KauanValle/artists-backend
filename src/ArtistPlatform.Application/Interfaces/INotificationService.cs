using ArtistPlatform.Application.DTOs;
using ArtistPlatform.Application.Persistence;
using ArtistPlatform.Domain.Entities;
using ArtistPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtistPlatform.Application.Interfaces;

/// <summary>Notificações in-app do usuário (PRD §23).</summary>
public interface INotificationService
{
    Task NotifyAsync(Guid userId, NotificationType type, string title, string message, string? link = null);
    Task<List<NotificationDto>> ListForUserAsync(bool? unreadOnly);
    Task<int> UnreadCountAsync();
    Task MarkReadAsync(Guid id);
    Task MarkAllReadAsync();
}

public class NotificationService(IAppDbContext db, ICurrentUserService currentUser) : INotificationService
{
    public async Task NotifyAsync(Guid userId, NotificationType type, string title, string message, string? link = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Link = link
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<NotificationDto>> ListForUserAsync(bool? unreadOnly)
    {
        var userId = currentUser.EnsureAuthenticated();
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.Link, n.IsRead, n.CreatedAt))
            .ToListAsync();
    }

    public Task<int> UnreadCountAsync()
    {
        var userId = currentUser.EnsureAuthenticated();
        return db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkReadAsync(Guid id)
    {
        var userId = currentUser.EnsureAuthenticated();
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId)
            ?? throw new Common.NotFoundException("Notificação não encontrada.");
        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync()
    {
        var userId = currentUser.EnsureAuthenticated();
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}
