using Microsoft.EntityFrameworkCore;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class CommentService(ScrumDbContext db, INotificationService notifications) : ICommentService
{
    private const int MaxTextLength = 2000;

    public async Task<Result<List<CommentDto>>> ListAsync(int projectId, int storyId)
    {
        if (!await db.Stories.AnyAsync(s => s.Id == storyId && s.ProjectId == projectId)) return Error.NotFound();
        var comments = await db.Comments.AsNoTracking().Where(c => c.StoryId == storyId)
            .OrderBy(c => c.CreatedAt).Join(db.Users.AsNoTracking(), c => c.AuthorId, u => u.Id,
                (c, u) => new CommentDto(c.Id, c.StoryId, c.AuthorId, u.DisplayName, u.Color, c.Text, c.CreatedAt, c.EditedAt))
            .ToListAsync();
        return comments;
    }

    public async Task<Result<CommentDto>> CreateAsync(int projectId, int storyId, string authorId, string text)
    {
        var story = await db.Stories.AsNoTracking().Where(s => s.Id == storyId && s.ProjectId == projectId)
            .Select(s => new { s.TenantId, s.Title, s.AssigneeId, s.Number }).FirstOrDefaultAsync();
        if (story is null) return Error.NotFound();

        var trimmed = text.Trim();
        if (trimmed.Length == 0) return Error.Invalid("El comentario no puede estar vacío.");
        if (trimmed.Length > MaxTextLength) return Error.Invalid($"El comentario no puede superar {MaxTextLength} caracteres.");

        var author = await db.Users.AsNoTracking().Where(u => u.Id == authorId).Select(u => new { u.DisplayName, u.Color }).FirstAsync();
        var comment = new Comment { TenantId = story.TenantId, StoryId = storyId, AuthorId = authorId, Text = trimmed };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        await NotifyParticipants(projectId, storyId, story.Title, story.AssigneeId, authorId, author.DisplayName);
        return new CommentDto(comment.Id, storyId, authorId, author.DisplayName, author.Color, trimmed, comment.CreatedAt, null);
    }

    public async Task<Result> DeleteAsync(int projectId, int storyId, int id, string actorId, bool isModerator)
    {
        if (!await db.Stories.AnyAsync(s => s.Id == storyId && s.ProjectId == projectId)) return Error.NotFound();
        var query = db.Comments.Where(c => c.Id == id && c.StoryId == storyId);
        if (!isModerator) query = query.Where(c => c.AuthorId == actorId);
        return await query.ExecuteDeleteAsync() == 0 ? Error.NotFound() : Result.Success();
    }

    /// <summary>Notifica al responsable de la historia y a quien ya haya comentado en ella, sin repetir ni avisarse a sí mismo.</summary>
    private async Task NotifyParticipants(int projectId, int storyId, string storyTitle, string? assigneeId, string authorId, string authorName)
    {
        var link = $"/p/{projectId}/backlog?story={storyId}";
        var text = $"{authorName} comentó en \"{storyTitle}\".";

        var recipients = await db.Comments.AsNoTracking().Where(c => c.StoryId == storyId).Select(c => c.AuthorId).Distinct().ToListAsync();
        if (assigneeId is not null) recipients.Add(assigneeId);

        foreach (var userId in recipients.Distinct())
            await notifications.NotifyAsync(userId, NotificationType.Comment, text, link, excludeUserId: authorId);
    }
}
