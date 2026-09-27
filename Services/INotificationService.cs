using Scrum.Api.Controllers;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public interface INotificationService
{
    /// <summary>Guarda el aviso y lo empuja por el hub. No hace nada si userId == excludeUserId (no te avisas a ti mismo).</summary>
    Task NotifyAsync(string userId, NotificationType type, string text, string link, string? excludeUserId);
    Task<List<NotificationDto>> ListAsync(string userId);
    Task<Result> MarkReadAsync(string userId, int id);
    Task MarkAllReadAsync(string userId);
}
