using Microsoft.EntityFrameworkCore;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class StoryService(ScrumDbContext db, INotificationService notifications) : IStoryService
{
    /// <summary>Escala de Fibonacci de Planning Poker. Null = sin estimar.</summary>
    private static readonly int[] PointScale = [1, 2, 3, 5, 8, 13, 21];
    private const double OrderGap = 1024;

    public async Task<Result<List<StoryDto>>> ListAsync(int projectId)
    {
        var key = await db.Projects.Where(p => p.Id == projectId).Select(p => p.Key).FirstOrDefaultAsync();
        if (key is null) return Error.NotFound();
        var stories = await db.Stories.AsNoTracking().Include(s => s.Criteria)
            .Where(s => s.ProjectId == projectId).OrderBy(s => s.Order).ThenBy(s => s.Id).ToListAsync();
        return stories.Select(s => ToDto(s, key)).ToList();
    }

    public async Task<List<StoryDto>> ListAllAsync(string? assigneeId)
    {
        var query = db.Stories.AsNoTracking().Include(s => s.Criteria).AsQueryable();
        if (assigneeId is not null) query = query.Where(s => s.AssigneeId == assigneeId);
        var keys = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Key);
        var stories = await query.OrderBy(s => s.ProjectId).ThenBy(s => s.Order).ThenBy(s => s.Id).ToListAsync();
        return stories.Select(s => ToDto(s, keys[s.ProjectId])).ToList();
    }

    public async Task<Result<StoryDto>> CreateAsync(int projectId, StoryInput input, string actorId)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project is null) return Error.NotFound();
        if (await Validate(projectId, input) is { } error) return error;

        var story = new Story { TenantId = project.TenantId, ProjectId = projectId, Number = project.NextStoryNumber++, Title = input.Title.Trim() };
        Apply(story, input);
        SyncCriteria(story, input.Criteria);
        var max = await db.Stories.Where(s => s.ProjectId == projectId && s.SprintId == story.SprintId && s.Status == story.Status)
            .MaxAsync(s => (double?)s.Order) ?? 0;
        story.Order = max + OrderGap;

        db.Stories.Add(story);
        await db.SaveChangesAsync();
        if (story.AssigneeId is not null)
            await notifications.NotifyAsync(story.AssigneeId, NotificationType.Assigned,
                $"Te asignaron \"{story.Title}\".", $"/p/{projectId}/backlog?story={story.Id}", excludeUserId: actorId);
        return ToDto(story, project.Key);
    }

    public async Task<Result> UpdateAsync(int projectId, int id, StoryInput input, string actorId)
    {
        var story = await db.Stories.Include(s => s.Criteria).FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
        if (story is null) return Error.NotFound();
        if (await Validate(projectId, input) is { } error) return error;

        var previousAssignee = story.AssigneeId;
        story.Title = input.Title.Trim();
        Apply(story, input);
        SyncCriteria(story, input.Criteria);
        await db.SaveChangesAsync();

        if (story.AssigneeId is not null && story.AssigneeId != previousAssignee)
            await notifications.NotifyAsync(story.AssigneeId, NotificationType.Assigned,
                $"Te asignaron \"{story.Title}\".", $"/p/{projectId}/backlog?story={story.Id}", excludeUserId: actorId);
        return Result.Success();
    }

    public async Task<Result> MoveAsync(int projectId, int id, MoveInput input)
    {
        var story = await db.Stories.FirstOrDefaultAsync(s => s.Id == id && s.ProjectId == projectId);
        if (story is null) return Error.NotFound();
        if (await CheckSprint(projectId, input.SprintId) is { } error) return error;

        story.SprintId = input.SprintId;
        SetStatus(story, input.Status);
        story.Order = input.Order;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int projectId, int id) =>
        await db.Stories.Where(s => s.Id == id && s.ProjectId == projectId).ExecuteDeleteAsync() == 0 ? Error.NotFound() : Result.Success();

    private static void Apply(Story s, StoryInput i)
    {
        s.Description = i.Description;
        s.Type = i.Type;
        s.Priority = i.Priority;
        s.Points = i.Points;
        s.SprintId = i.SprintId;
        s.AssigneeId = string.IsNullOrEmpty(i.AssigneeId) ? null : i.AssigneeId;
        SetStatus(s, i.Status);
    }

    /// <summary>Un item del backlog no puede estar en curso; Done fija la fecha de cierre que alimenta el burndown.</summary>
    private static void SetStatus(Story s, StoryStatus status)
    {
        if (s.SprintId is null) status = StoryStatus.Todo;
        s.CompletedAt = status == StoryStatus.Done ? s.CompletedAt ?? DateTime.UtcNow : null;
        s.Status = status;
    }

    private static void SyncCriteria(Story story, List<CriterionDto>? incoming)
    {
        if (incoming is null) return;
        var keep = incoming.Where(c => c.Id is not null).Select(c => c.Id!.Value).ToHashSet();
        story.Criteria.RemoveAll(c => !keep.Contains(c.Id));

        for (var i = 0; i < incoming.Count; i++)
        {
            var c = incoming[i];
            var text = c.Text.Trim();
            if (text.Length == 0) continue;
            var existing = c.Id is null ? null : story.Criteria.FirstOrDefault(x => x.Id == c.Id);
            if (existing is null) story.Criteria.Add(new AcceptanceCriterion { TenantId = story.TenantId, Text = text, Done = c.Done, Order = i });
            else { existing.Text = text; existing.Done = c.Done; existing.Order = i; }
        }
    }

    private async Task<Error?> Validate(int projectId, StoryInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Title)) return Error.Invalid("El título es obligatorio.");
        if (i.Points is { } p && !PointScale.Contains(p)) return Error.Invalid($"Los puntos deben ser {string.Join(", ", PointScale)} o vacío.");
        if (await CheckSprint(projectId, i.SprintId) is { } sprintError) return sprintError;
        if (!string.IsNullOrEmpty(i.AssigneeId) && !await db.Users.AnyAsync(u => u.Id == i.AssigneeId))
            return Error.Invalid("El responsable no existe.");
        return null;
    }

    private async Task<Error?> CheckSprint(int projectId, int? sprintId)
    {
        if (sprintId is not { } sid) return null;
        var status = await db.Sprints.Where(s => s.Id == sid && s.ProjectId == projectId).Select(s => (SprintStatus?)s.Status).FirstOrDefaultAsync();
        if (status is null) return Error.Invalid("El sprint no pertenece a este proyecto.");
        if (status == SprintStatus.Completed) return Error.Invalid("No se pueden asignar historias a un sprint cerrado.");
        return null;
    }

    private static StoryDto ToDto(Story s, string projectKey) => new(
        s.Id, s.ProjectId, s.Number, $"{projectKey}-{s.Number}", s.Title, s.Description,
        s.Type, s.Status, s.Priority, s.Points, s.SprintId, s.AssigneeId,
        s.Order, s.CreatedAt, s.CompletedAt,
        [.. s.Criteria.OrderBy(c => c.Order).Select(c => new CriterionDto(c.Id, c.Text, c.Done))]);
}
