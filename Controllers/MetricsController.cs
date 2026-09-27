using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

/// <param name="AvgCycleTimeDays">Null si no hubo historias cerradas en los últimos 30 días.</param>
public record SummaryDto(
    Dictionary<StoryStatus, int> ByStatus, Dictionary<StoryType, int> ByType, Dictionary<Priority, int> ByPriority,
    double? AvgCycleTimeDays);

/// <param name="Points">Suma de puntos de las historias que quedaron enlazadas al sprint al cerrarlo (solo las Done).</param>
/// <param name="Capacity">Puntos que el equipo se había comprometido a completar (Sprint.Capacity).</param>
public record VelocityPointDto(int SprintId, string Name, int Points, int Capacity);

/// <param name="Remaining">Puntos que faltaban al cierre de ese día (o al momento de la consulta, si es hoy).</param>
public record BurndownPointDto(DateOnly Date, int Remaining);

/// <summary>Lectura pura calculada desde Story/Sprint, sin tablas propias. Ver limitaciones en IMetricsService.</summary>
[ApiController]
[Route("api/projects/{projectId:int}/metrics")]
[Produces("application/json")]
public class MetricsController(IMetricsService metrics) : ControllerBase
{
    /// <summary>Distribución de historias por estado/tipo/prioridad y tiempo de ciclo promedio (últimos 30 días).</summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SummaryDto>> Summary(int projectId) => this.ToOk(await metrics.SummaryAsync(projectId));

    /// <summary>Puntos completados de los últimos 6 sprints cerrados.</summary>
    [HttpGet("velocity")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<VelocityPointDto>>> Velocity(int projectId) => this.ToOk(await metrics.VelocityAsync(projectId));

    /// <summary>Puntos restantes día a día de un sprint. Preciso solo mientras el sprint está activo (ver
    /// limitaciones documentadas en IMetricsService).</summary>
    [HttpGet("burndown")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<BurndownPointDto>>> Burndown(int projectId, [FromQuery] int sprintId) =>
        this.ToOk(await metrics.BurndownAsync(projectId, sprintId));
}
