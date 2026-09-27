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
public class StoryTools(IStoryService stories, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista las historias de un proyecto, ordenadas por posición.")]
    public async Task<List<StoryDto>> ListStories(McpServer server, [Description("Id del proyecto.")] int projectId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await stories.ListAsync(projectId);
        Guard(result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Lista historias de todos los proyectos de la organización actual.")]
    public async Task<List<StoryDto>> ListAllStories(McpServer server,
        [Description("'me' para solo las asignadas al usuario de la sesión, o vacío para todas.")] string? assignee = null)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        return await stories.ListAllAsync(assignee == "me" ? user.UserId() : string.IsNullOrEmpty(assignee) ? null : assignee);
    }

    [McpServerTool, Description("Crea una historia al final de su columna. Requiere rol distinto de Viewer.")]
    public async Task<StoryDto> CreateStory(McpServer server,
        [Description("Id del proyecto.")] int projectId,
        [Description("Título de la historia.")] string title,
        [Description("Descripción, opcional.")] string? description = null,
        [Description("Story, Bug, Task o Spike.")] StoryType type = StoryType.Story,
        [Description("Todo, InProgress, Review o Done.")] StoryStatus status = StoryStatus.Todo,
        [Description("Low, Medium, High o Critical.")] Priority priority = Priority.Medium,
        [Description("Puntos Fibonacci (1,2,3,5,8,13,21) o null.")] int? points = null,
        [Description("Id del sprint, o null para el backlog.")] int? sprintId = null,
        [Description("Id del usuario responsable, o null.")] string? assigneeId = null)
    {
        var user = await Require(server);
        var result = await stories.CreateAsync(projectId, new StoryInput(title, description, type, status, priority, points, sprintId, assigneeId, null), user.UserId());
        Guard(result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Actualiza una historia (no toca sus criterios de aceptación; usar la web para eso). Requiere rol distinto de Viewer.")]
    public async Task<string> UpdateStory(McpServer server,
        [Description("Id del proyecto.")] int projectId, [Description("Id de la historia.")] int storyId,
        [Description("Título.")] string title,
        [Description("Descripción, opcional.")] string? description = null,
        [Description("Story, Bug, Task o Spike.")] StoryType type = StoryType.Story,
        [Description("Todo, InProgress, Review o Done.")] StoryStatus status = StoryStatus.Todo,
        [Description("Low, Medium, High o Critical.")] Priority priority = Priority.Medium,
        [Description("Puntos Fibonacci o null.")] int? points = null,
        [Description("Id del sprint, o null para el backlog.")] int? sprintId = null,
        [Description("Id del usuario responsable, o null.")] string? assigneeId = null)
    {
        var user = await Require(server);
        var result = await stories.UpdateAsync(projectId, storyId, new StoryInput(title, description, type, status, priority, points, sprintId, assigneeId, null), user.UserId());
        Guard(result.Error);
        return "Actualizada.";
    }

    [McpServerTool, Description("Mueve una historia a otro estado, sprint o posición (drag & drop del tablero). Requiere rol distinto de Viewer.")]
    public async Task<string> MoveStory(McpServer server,
        [Description("Id del proyecto.")] int projectId, [Description("Id de la historia.")] int storyId,
        [Description("Nuevo estado.")] StoryStatus status,
        [Description("Nuevo sprint, o null para el backlog.")] int? sprintId,
        [Description("Nueva posición dentro de la columna (menor = más arriba).")] double order)
    {
        var user = await Require(server);
        var result = await stories.MoveAsync(projectId, storyId, new MoveInput(status, sprintId, order));
        Guard(result.Error);
        return "Movida.";
    }

    private async Task<System.Security.Claims.ClaimsPrincipal> Require(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        await PolicyGuard.RequireAsync(authz, user, Policies.Contribute);
        return user;
    }

    private static void Guard(Error? error)
    {
        if (error is not null) throw new McpException(error.Message.Length > 0 ? error.Message : "No encontrado.");
    }
}
