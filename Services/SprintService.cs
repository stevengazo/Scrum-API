using Microsoft.EntityFrameworkCore;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class SprintService(ScrumDbContext db, INotificationService notifications) : ISprintService
{
    public async Task<IReadOnlyList<Sprint>> ListAsync(int projectId) =>
        await db.Sprints.AsNoTracking().Where(s => s.ProjectId == projectId).OrderBy(s => s.StartDate).ThenBy(s => s.Id).ToListAsync();

    public async Task<Result<Sprint>> CreateAsync(int projectId, SprintInput input)
    {
        var tenantId = await db.Projects.Where(p => p.Id == projectId).Select(p => (int?)p.TenantId).FirstOrDefaultAsync();
        if (tenantId is null) return Error.NotFound();
        if (Validate(input) is { } error) return error;
        var s = new Sprint
        {
            TenantId = tenantId.Value, ProjectId = projectId, Name = input.Name.Trim(), Goal = input.Goal,
            StartDate = input.StartDate, EndDate = input.EndDate, Capacity = input.Capacity,
        };
        db.Sprints.Add(s);
        await db.SaveChangesAsync();
        return s;
    }

    public async Task<Result> UpdateAsync(int projectId, int id, SprintInput input)
    {
        var s = await Find(projectId, id);
        if (s is null) return Error.NotFound();
        if (Validate(input) is { } error) return error;
        s.Name = input.Name.Trim(); s.Goal = input.Goal;
        s.StartDate = input.StartDate; s.EndDate = input.EndDate; s.Capacity = input.Capacity;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> StartAsync(int projectId, int id)
    {
        var s = await Find(projectId, id);
        if (s is null) return Error.NotFound();
        if (s.Status != SprintStatus.Planned) return Error.Invalid("Solo se puede iniciar un sprint planificado.");
        if (await db.Sprints.AnyAsync(x => x.ProjectId == projectId && x.Status == SprintStatus.Active))
            return Error.Invalid("Ya hay un sprint activo en este proyecto.");
        s.Status = SprintStatus.Active;
        await db.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>Lo no terminado vuelve al backlog y compite de nuevo por prioridad.</summary>
    public async Task<Result<int>> CompleteAsync(int projectId, int id, string actorId)
    {
        var s = await Find(projectId, id);
        if (s is null) return Error.NotFound();
        if (s.Status != SprintStatus.Active) return Error.Invalid("Solo se puede cerrar un sprint activo.");

        var assignees = await db.Stories.AsNoTracking().Where(x => x.SprintId == id && x.AssigneeId != null)
            .Select(x => x.AssigneeId!).Distinct().ToListAsync();
        var returned = await ReturnToBacklog(id, notDoneOnly: true);
        s.Status = SprintStatus.Completed;
        await db.SaveChangesAsync();

        foreach (var userId in assignees)
            await notifications.NotifyAsync(userId, NotificationType.SprintCompleted,
                $"Se cerró el sprint \"{s.Name}\".", $"/p/{projectId}/sprints", excludeUserId: actorId);
        return returned;
    }

    public async Task<Result> DeleteAsync(int projectId, int id)
    {
        var s = await Find(projectId, id);
        if (s is null) return Error.NotFound();
        if (s.Status == SprintStatus.Completed) return Error.Invalid("Un sprint cerrado forma parte del historial y no se elimina.");
        await ReturnToBacklog(id, notDoneOnly: false);
        db.Sprints.Remove(s);
        await db.SaveChangesAsync();
        return Result.Success();
    }

    private Task<int> ReturnToBacklog(int sprintId, bool notDoneOnly) =>
        db.Stories.Where(x => x.SprintId == sprintId && (!notDoneOnly || x.Status != StoryStatus.Done))
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.SprintId, (int?)null).SetProperty(x => x.Status, StoryStatus.Todo));

    private Task<Sprint?> Find(int projectId, int id) => db.Sprints.FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId);

    private static Error? Validate(SprintInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Name)) return Error.Invalid("El nombre es obligatorio.");
        if (i.EndDate < i.StartDate) return Error.Invalid("La fecha de fin debe ser igual o posterior al inicio.");
        if (i.Capacity < 0) return Error.Invalid("La capacidad no puede ser negativa.");
        return null;
    }
}
