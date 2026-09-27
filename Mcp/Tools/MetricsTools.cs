using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Controllers;
using Scrum.Api.Services;

namespace Scrum.Api.Mcp.Tools;

[McpServerToolType]
public class MetricsTools(IMetricsService metrics, McpSessionStore sessions, IHttpContextAccessor accessor)
{
    [McpServerTool, Description("Resumen de un proyecto: historias por estado/tipo/prioridad y tiempo de ciclo promedio (últimos 30 días).")]
    public async Task<SummaryDto> GetSummary(McpServer server, [Description("Id del proyecto.")] int projectId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await metrics.SummaryAsync(projectId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return result.Value!;
    }

    [McpServerTool, Description("Puntos completados de los últimos 6 sprints cerrados de un proyecto.")]
    public async Task<List<VelocityPointDto>> GetVelocity(McpServer server, [Description("Id del proyecto.")] int projectId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await metrics.VelocityAsync(projectId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return result.Value!;
    }

    [McpServerTool, Description("Puntos restantes día a día de un sprint. Solo preciso mientras el sprint está activo.")]
    public async Task<List<BurndownPointDto>> GetBurndown(McpServer server, [Description("Id del proyecto.")] int projectId, [Description("Id del sprint.")] int sprintId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await metrics.BurndownAsync(projectId, sprintId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return result.Value!;
    }
}
