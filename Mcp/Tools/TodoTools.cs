using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Services;

namespace Scrum.Api.Mcp.Tools;

/// <summary>La lista to-do es personal: no hay política de rol, cada quien administra solo la suya.</summary>
[McpServerToolType]
public class TodoTools(ITodoService todos, McpSessionStore sessions, IHttpContextAccessor accessor)
{
    [McpServerTool, Description("Lista los items de la lista to-do del usuario de la sesión: pendientes primero.")]
    public async Task<List<TodoDto>> ListTodos(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        return await todos.ListAsync(user.UserId());
    }

    [McpServerTool, Description("Agrega una nota libre o enlaza una historia (sin texto, usa el título de la historia).")]
    public async Task<TodoDto> CreateTodo(McpServer server,
        [Description("Texto de la nota. Opcional si se da storyId.")] string? text = null,
        [Description("Id de la historia a enlazar, o null para una nota libre.")] int? storyId = null)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        var result = await todos.CreateAsync(user.UserId(), new TodoCreateInput(text, storyId));
        if (result.Error is not null) throw new McpException(result.Error.Message);
        return result.Value!;
    }

    [McpServerTool, Description("Edita el texto o marca/desmarca como hecho un item de la lista to-do.")]
    public async Task<string> UpdateTodo(McpServer server,
        [Description("Id del item.")] int todoId, [Description("Nuevo texto.")] string text, [Description("true si está hecho.")] bool done)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        var result = await todos.UpdateAsync(user.UserId(), todoId, new TodoUpdateInput(text, done));
        if (result.Error is not null) throw new McpException(result.Error.Message.Length > 0 ? result.Error.Message : "No encontrado.");
        return "Actualizado.";
    }

    [McpServerTool, Description("Suma un pomodoro completado a un item de la lista to-do.")]
    public async Task<TodoDto> AddPomodoro(McpServer server, [Description("Id del item.")] int todoId)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        var result = await todos.AddPomodoroAsync(user.UserId(), todoId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return result.Value!;
    }
}
