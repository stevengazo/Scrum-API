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
public class ProjectTools(IProjectService projects, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista los proyectos de la organización actual, con su avance (historias totales y terminadas).")]
    public async Task<IReadOnlyList<ProjectDto>> ListProjects(McpServer server)
    {
        PrincipalScope.Require(server, sessions, accessor);
        return await projects.ListAsync();
    }

    [McpServerTool, Description("Obtiene un proyecto por id.")]
    public async Task<ProjectDto> GetProject(McpServer server, [Description("Id del proyecto.")] int projectId)
    {
        Require(server);
        var result = await projects.GetAsync(projectId);
        Guard(server, result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Crea un proyecto. Requiere rol Admin, ScrumMaster o ProductOwner.")]
    public async Task<ProjectDto> CreateProject(McpServer server,
        [Description("Prefijo de las claves de historia (2-8 caracteres, A-Z/0-9, empieza con letra). Ej.: AGL.")] string key,
        [Description("Nombre del proyecto.")] string name,
        [Description("Descripción opcional.")] string? description = null,
        [Description("Color hex opcional, ej. #6366f1.")] string? color = null)
    {
        var user = Require(server);
        await PolicyGuard.RequireAsync(authz, user, Policies.ManageProjects);
        var result = await projects.CreateAsync(new ProjectCreateInput(key, name, description, color), user.TenantId());
        Guard(server, result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Actualiza nombre, descripción, color y estado de archivado de un proyecto.")]
    public async Task<string> UpdateProject(McpServer server,
        [Description("Id del proyecto.")] int projectId,
        [Description("Nuevo nombre.")] string name,
        [Description("Nueva descripción, o null para dejarla vacía.")] string? description = null,
        [Description("Nuevo color hex.")] string? color = null,
        [Description("true para archivarlo.")] bool archived = false)
    {
        var user = Require(server);
        await PolicyGuard.RequireAsync(authz, user, Policies.ManageProjects);
        var result = await projects.UpdateAsync(projectId, new ProjectUpdateInput(name, description, color, archived));
        Guard(server, result.Error);
        return "Actualizado.";
    }

    private System.Security.Claims.ClaimsPrincipal Require(McpServer server) => PrincipalScope.Require(server, sessions, accessor);

    private static void Guard(McpServer server, Error? error)
    {
        if (error is not null) throw new McpException(error.Message.Length > 0 ? error.Message : "No encontrado.");
    }
}
