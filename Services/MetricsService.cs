using Microsoft.EntityFrameworkCore;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

/// <summary>
/// Todo se calcula al vuelo desde Story/Sprint, sin tablas propias.
/// <para><b>Límite del burndown:</b> solo es preciso para el sprint activo. No hay tabla de snapshots que registre
/// el alcance día a día, así que se compara contra los puntos que hoy quedan enlazados al sprint.</para>
/// <para><b>Límite de la velocidad:</b> al cerrar un sprint, SprintService.CompleteAsync devuelve al backlog lo no
/// terminado; solo las historias Done siguen con ese SprintId, así que "velocidad" = la suma de esos puntos, no lo
/// que se comprometió originalmente (ese dato no se guarda).</para>
/// </summary>
public class MetricsService(ScrumDbContext db) : IMetricsService
{
    private const int VelocitySprintCount = 6;
    private const int CycleTimeWindowDays = 30;

    public async Task<Result<SummaryDto>> SummaryAsync(int projectId)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Error.NotFound();

        var stories = await db.Stories.AsNoTracking().Where(s => s.ProjectId == projectId)
            .Select(s => new { s.Status, s.Type, s.Priority, s.CreatedAt, s.CompletedAt }).ToListAsync();

        var byStatus = Enum.GetValues<StoryStatus>().ToDictionary(v => v, v => stories.Count(s => s.Status == v));
        var byType = Enum.GetValues<StoryType>().ToDictionary(v => v, v => stories.Count(s => s.Type == v));
        var byPriority = Enum.GetValues<Priority>().ToDictionary(v => v, v => stories.Count(s => s.Priority == v));

        var since = DateTime.UtcNow.AddDays(-CycleTimeWindowDays);
        var recentlyDone = stories.Where(s => s.CompletedAt is { } c && c >= since).Select(s => (s.CompletedAt!.Value - s.CreatedAt).TotalDays).ToList();
        double? avgCycleTime = recentlyDone.Count == 0 ? null : Math.Round(recentlyDone.Average(), 1);

        return new SummaryDto(byStatus, byType, byPriority, avgCycleTime);
    }

    public async Task<Result<List<VelocityPointDto>>> VelocityAsync(int projectId)
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Error.NotFound();

        var sprints = await db.Sprints.AsNoTracking().Where(s => s.ProjectId == projectId && s.Status == SprintStatus.Completed)
            .OrderByDescending(s => s.EndDate).Take(VelocitySprintCount)
            .Select(s => new { s.Id, s.Name, s.Capacity }).ToListAsync();
        if (sprints.Count == 0) return new List<VelocityPointDto>();

        var sprintIds = sprints.Select(s => s.Id).ToList();
        var points = await db.Stories.AsNoTracking().Where(s => s.SprintId != null && sprintIds.Contains(s.SprintId.Value))
            .GroupBy(s => s.SprintId!.Value).Select(g => new { SprintId = g.Key, Points = g.Sum(s => s.Points ?? 0) }).ToDictionaryAsync(x => x.SprintId, x => x.Points);

        // Del más antiguo al más nuevo: se lee como una línea de tiempo.
        return sprints.AsEnumerable().Reverse()
            .Select(s => new VelocityPointDto(s.Id, s.Name, points.GetValueOrDefault(s.Id), s.Capacity)).ToList();
    }

    public async Task<Result<List<BurndownPointDto>>> BurndownAsync(int projectId, int sprintId)
    {
        var sprint = await db.Sprints.AsNoTracking().Where(s => s.Id == sprintId && s.ProjectId == projectId)
            .Select(s => new { s.StartDate, s.EndDate }).FirstOrDefaultAsync();
        if (sprint is null) return Error.NotFound();

        var stories = await db.Stories.AsNoTracking().Where(s => s.SprintId == sprintId)
            .Select(s => new { Points = s.Points ?? 0, s.CompletedAt }).ToListAsync();
        var total = stories.Sum(s => s.Points);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lastDay = sprint.EndDate < today ? sprint.EndDate : today;
        if (lastDay < sprint.StartDate) lastDay = sprint.StartDate;

        var result = new List<BurndownPointDto>();
        for (var day = sprint.StartDate; day <= lastDay; day = day.AddDays(1))
        {
            var doneByThen = stories.Where(s => s.CompletedAt is { } c && DateOnly.FromDateTime(c) <= day).Sum(s => s.Points);
            result.Add(new BurndownPointDto(day, total - doneByThen));
        }
        return result;
    }
}
