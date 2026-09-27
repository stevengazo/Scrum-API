using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Mcp.Tools;

[McpServerToolType]
public class SprintTools(ISprintService sprints, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista los sprints de un proyecto, por fecha de inicio.")]
    public async Task<IReadOnlyList<Sprint>> ListSprints(McpServer server, [Description("Id del proyecto.")] int projectId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        return await sprints.ListAsync(projectId);
    }

    [McpServerTool, Description("Crea un sprint planificado. Requiere Admin o ScrumMaster.")]
    public async Task<Sprint> CreateSprint(McpServer server,
        [Description("Id del proyecto.")] int projectId,
        [Description("Nombre del sprint.")] string name,
        [Description("Fecha de inicio (yyyy-MM-dd).")] DateOnly startDate,
        [Description("Fecha de fin (yyyy-MM-dd), igual o posterior a la de inicio.")] DateOnly endDate,
        [Description("Puntos que el equipo se compromete a completar.")] int capacity,
        [Description("Objetivo del sprint, opcional.")] string? goal = null)
    {
        var user = await Require(server);
        var result = await sprints.CreateAsync(projectId, new SprintInput(name, goal, startDate, endDate, capacity));
        Guard(result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Actualiza los datos de un sprint. Requiere Admin o ScrumMaster.")]
    public async Task<string> UpdateSprint(McpServer server,
        [Description("Id del proyecto.")] int projectId, [Description("Id del sprint.")] int sprintId,
        [Description("Nombre del sprint.")] string name,
        [Description("Fecha de inicio (yyyy-MM-dd).")] DateOnly startDate,
        [Description("Fecha de fin (yyyy-MM-dd).")] DateOnly endDate,
        [Description("Capacidad en puntos.")] int capacity,
        [Description("Objetivo, opcional.")] string? goal = null)
    {
        await Require(server);
        var result = await sprints.UpdateAsync(projectId, sprintId, new SprintInput(name, goal, startDate, endDate, capacity));
        Guard(result.Error);
        return "Actualizado.";
    }

    [McpServerTool, Description("Inicia un sprint planificado. Solo puede haber uno activo por proyecto. Requiere Admin o ScrumMaster.")]
    public async Task<string> StartSprint(McpServer server, [Description("Id del proyecto.")] int projectId, [Description("Id del sprint.")] int sprintId)
    {
        await Require(server);
        var result = await sprints.StartAsync(projectId, sprintId);
        Guard(result.Error);
        return "Sprint iniciado.";
    }

    [McpServerTool, Description("Cierra un sprint activo: lo no terminado vuelve al backlog. Notifica a los responsables afectados. Requiere Admin o ScrumMaster.")]
    public async Task<string> CompleteSprint(McpServer server, [Description("Id del proyecto.")] int projectId, [Description("Id del sprint.")] int sprintId)
    {
        var user = await Require(server);
        var result = await sprints.CompleteAsync(projectId, sprintId, user.UserId());
        Guard(result.Error);
        return $"Sprint cerrado. {result.Value} historia(s) volvieron al backlog.";
    }

    private async Task<System.Security.Claims.ClaimsPrincipal> Require(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        await PolicyGuard.RequireAsync(authz, user, Policies.ManageSprints);
        return user;
    }

    private static void Guard(Error? error)
    {
        if (error is not null) throw new McpException(error.Message.Length > 0 ? error.Message : "No encontrado.");
    }
}
