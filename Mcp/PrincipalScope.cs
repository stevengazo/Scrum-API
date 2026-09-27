using System.Security.Claims;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Scrum.Api.Mcp;

/// <summary>Traduce la McpSession de esta conexión a un ClaimsPrincipal idéntico en forma al que emite
/// TokenService.Create, y lo pone en el HttpContext ambiente. A partir de ahí, User.TenantId()/UserId(),
/// ICurrentTenant (y por lo tanto el query filter global de ScrumDbContext) y las políticas de autorización
/// funcionan exactamente igual que en un request REST autenticado — sin duplicar esa lógica para MCP.</summary>
public static class PrincipalScope
{
    /// <summary>Exige que la conexión ya haya hecho `login`; si no, McpException explica qué tool llamar.</summary>
    public static ClaimsPrincipal Require(McpServer server, McpSessionStore store, IHttpContextAccessor accessor)
    {
        var sessionId = server.SessionId
            ?? throw new McpException("Esta conexión no tiene sesión MCP (SessionId nulo); revisá el transporte.");
        var session = store.Get(sessionId)
            ?? throw new McpException("Iniciá sesión primero con la tool 'login'.");

        // roleType "role": debe coincidir con RoleClaimType configurado para el JWT (Auth/Auth.cs), o
        // User.IsInRole/RequireRole no encontrarían los claims (su tipo por defecto es otra URI).
        var identity = new ClaimsIdentity("mcp", "name", "role");
        identity.AddClaim(new Claim("sub", session.UserId));
        identity.AddClaim(new Claim("tid", session.TenantId.ToString()));
        identity.AddClaim(new Claim("email", session.Email));
        identity.AddClaim(new Claim("name", session.DisplayName));
        foreach (var role in session.Roles) identity.AddClaim(new Claim("role", role));

        var principal = new ClaimsPrincipal(identity);
        if (accessor.HttpContext is not null) accessor.HttpContext.User = principal;
        return principal;
    }
}
