using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class NotificationService(ScrumDbContext db, IHubContext<NotificationsHub> hub, ICurrentTenant currentTenant) : INotificationService
{
    private const int MaxList = 50;

    public async Task NotifyAsync(string userId, NotificationType type, string text, string link, string? excludeUserId)
    {
        if (userId == excludeUserId) return;

        // El emisor y el destinatario están siempre en el mismo tenant (ambos vienen de consultas ya acotadas por
        // el filtro global), así que el tenant del aviso es el del request actual.
        var n = new Notification { TenantId = currentTenant.TenantId!.Value, UserId = userId, Type = type, Text = text, Link = link };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        await hub.Clients.Group(NotificationsHub.GroupFor(userId)).SendAsync("notification", ToDto(n));
    }

    public async Task<List<NotificationDto>> ListAsync(string userId) =>
        await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).Take(MaxList).Select(n => ToDto(n)).ToListAsync();

    public async Task<Result> MarkReadAsync(string userId, int id)
    {
        var updated = await db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(n => n.Read, true));
        return updated == 0 ? Error.NotFound() : Result.Success();
    }

    public Task MarkAllReadAsync(string userId) =>
        db.Notifications.Where(n => n.UserId == userId && !n.Read).ExecuteUpdateAsync(u => u.SetProperty(n => n.Read, true));

    private static NotificationDto ToDto(Notification n) => new(n.Id, n.Type, n.Text, n.Link, n.Read, n.CreatedAt);
}
