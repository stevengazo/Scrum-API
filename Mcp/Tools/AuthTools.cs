using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Services;

namespace Scrum.Api.Mcp.Tools;

[McpServerToolType]
public class AuthTools(IAccountService accounts, McpSessionStore sessions, IConfiguration config)
{
    [McpServerTool, Description("Inicia sesión con email y contraseña. Hace falta llamar esto antes que cualquier " +
        "otra tool de este servidor (salvo register_organization y get_tenant_by_slug). La sesión dura mientras " +
        "dure esta conexión MCP; no hace falta repetirlo en cada tool.")]
    public async Task<string> Login(McpServer server,
        [Description("Correo de la cuenta.")] string email,
        [Description("Contraseña de la cuenta.")] string password)
    {
        var result = await accounts.LoginAsync(email, password);
        if (result.Error is not null) throw new McpException(result.Error.Message);

        var (roles, tenantName) = await accounts.IdentityAsync(result.Value!);
        sessions.Set(server.SessionId!, new McpSession(result.Value!.Id, result.Value!.TenantId, [.. roles], result.Value!.DisplayName, result.Value!.Email!));
        return $"Sesión iniciada como {result.Value!.DisplayName} ({string.Join(", ", roles)}) en la organización \"{tenantName}\".";
    }

    [McpServerTool, Description("Quién sos en esta sesión MCP: organización, nombre y roles.")]
    public string Whoami(McpServer server)
    {
        var session = sessions.Get(server.SessionId ?? "");
        return session is null
            ? "No hay sesión activa. Llamá a la tool 'login' primero."
            : $"{session.DisplayName} <{session.Email}> — roles: {string.Join(", ", session.Roles)}.";
    }

    [McpServerTool, Description("Crea una organización nueva (quien la crea queda Admin de ella) o se une a una " +
        "existente por su slug de invitación (entra como Viewer, salvo que sea el primer usuario de esa " +
        "organización). Después de esto, la sesión queda logueada como ese usuario — no hace falta llamar login.")]
    public async Task<string> RegisterOrganization(McpServer server,
        [Description("Correo del nuevo usuario.")] string email,
        [Description("Contraseña del nuevo usuario, mínimo 8 caracteres.")] string password,
        [Description("Nombre visible del nuevo usuario.")] string displayName,
        [Description("Nombre de la organización nueva. Obligatorio si no se da 'slug'.")] string? orgName = null,
        [Description("Slug de una organización existente a la que unirse (enlace de invitación). Si se da, orgName se ignora.")] string? slug = null)
    {
        var allow = config.GetValue("Auth:AllowRegistration", true);
        var result = await accounts.RegisterAsync(email, password, displayName, orgName, slug, allow);
        if (result.Error is not null) throw new McpException(result.Error.Message);

        sessions.Set(server.SessionId!, new McpSession(result.Value!.User.Id, result.Value!.User.TenantId, result.Value!.Roles, result.Value!.User.DisplayName, result.Value!.User.Email!));
        return $"Cuenta creada: {result.Value!.User.DisplayName} en \"{result.Value!.TenantName}\" con rol {result.Value!.Roles[0]}.";
    }
}
