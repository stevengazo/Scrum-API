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
public class PageTools(IPageService pages, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista solo los metadatos de todas las páginas (el árbol completo); pedí el contenido con get_page.")]
    public async Task<IReadOnlyList<PageSummary>> ListPages(McpServer server)
    {
        PrincipalScope.Require(server, sessions, accessor);
        return await pages.ListAsync();
    }

    [McpServerTool, Description("Obtiene una página con su contenido (JSON de Lexical, o de la hoja de cálculo si Kind es Sheet).")]
    public async Task<PageDto> GetPage(McpServer server, [Description("Id de la página (GUID).")] Guid pageId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await pages.GetAsync(pageId);
        Guard(result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Crea una página, opcionalmente hija de otra o de un proyecto. Requiere rol distinto de Viewer.")]
    public async Task<PageDto> CreatePage(McpServer server,
        [Description("Título, opcional.")] string? title = null,
        [Description("Icono (emoji), opcional.")] string? icon = null,
        [Description("Id de la página padre, o null.")] Guid? parentId = null,
        [Description("Id del proyecto al que pertenece, o null para una página global.")] int? projectId = null,
        [Description("Page (documento) o Sheet (hoja de cálculo). Por defecto Page.")] PageKind kind = PageKind.Page)
    {
        var user = await Require(server);
        var result = await pages.CreateAsync(new PageCreateInput(title, icon, parentId, projectId, kind), user.UserId());
        Guard(result.Error);
        return result.Value!;
    }

    [McpServerTool, Description("Guarda título, icono y contenido de una página. Requiere rol distinto de Viewer.")]
    public async Task<string> UpdatePage(McpServer server,
        [Description("Id de la página.")] Guid pageId,
        [Description("Título.")] string title,
        [Description("Icono, opcional.")] string? icon = null,
        [Description("Contenido: JSON serializado del EditorState de Lexical, o de la hoja si es Sheet.")] string? content = null)
    {
        await Require(server);
        var result = await pages.UpdateAsync(pageId, new PageUpdateInput(title, icon, content));
        Guard(result.Error);
        return "Actualizada.";
    }

    [McpServerTool, Description("Cambia el padre y la posición de una página. Rechaza ciclos. Requiere rol distinto de Viewer.")]
    public async Task<string> MovePage(McpServer server,
        [Description("Id de la página.")] Guid pageId,
        [Description("Nuevo padre, o null para que quede en la raíz.")] Guid? parentId,
        [Description("Nueva posición (menor = más arriba).")] double order)
    {
        await Require(server);
        var result = await pages.MoveAsync(pageId, new PageMoveInput(parentId, order));
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
