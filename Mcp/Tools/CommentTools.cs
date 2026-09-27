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
public class CommentTools(ICommentService comments, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista el hilo de comentarios de una historia, del más antiguo al más nuevo.")]
    public async Task<List<CommentDto>> ListComments(McpServer server, [Description("Id del proyecto.")] int projectId, [Description("Id de la historia.")] int storyId)
    {
        PrincipalScope.Require(server, sessions, accessor);
        var result = await comments.ListAsync(projectId, storyId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return result.Value!;
    }

    [McpServerTool, Description("Agrega un comentario a una historia. Notifica al responsable y a quien ya haya participado. Requiere rol distinto de Viewer.")]
    public async Task<CommentDto> CreateComment(McpServer server,
        [Description("Id del proyecto.")] int projectId, [Description("Id de la historia.")] int storyId,
        [Description("Texto del comentario, máx. 2000 caracteres.")] string text)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        await PolicyGuard.RequireAsync(authz, user, Policies.Contribute);
        var result = await comments.CreateAsync(projectId, storyId, user.UserId(), text);
        if (result.Error is not null) throw new McpException(result.Error.Message.Length > 0 ? result.Error.Message : "No encontrado.");
        return result.Value!;
    }
}
