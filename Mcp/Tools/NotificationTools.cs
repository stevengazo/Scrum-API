using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Services;

namespace Scrum.Api.Mcp.Tools;

[McpServerToolType]
public class NotificationTools(INotificationService notifications, McpSessionStore sessions, IHttpContextAccessor accessor)
{
    [McpServerTool, Description("Últimos 50 avisos del usuario de la sesión, más reciente primero.")]
    public async Task<List<NotificationDto>> ListNotifications(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        return await notifications.ListAsync(user.UserId());
    }

    [McpServerTool, Description("Marca un aviso como leído.")]
    public async Task<string> MarkNotificationRead(McpServer server, [Description("Id del aviso.")] int notificationId)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        var result = await notifications.MarkReadAsync(user.UserId(), notificationId);
        if (result.Error is not null) throw new McpException("No encontrado.");
        return "Marcado.";
    }

    [McpServerTool, Description("Marca todos los avisos del usuario de la sesión como leídos.")]
    public async Task<string> MarkAllNotificationsRead(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        await notifications.MarkAllReadAsync(user.UserId());
        return "Todos marcados como leídos.";
    }
}
