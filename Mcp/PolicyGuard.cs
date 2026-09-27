using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Security.Claims;

namespace Scrum.Api.Mcp;

/// <summary>Las tools no pasan por el pipeline de MVC, así que [Authorize(Policies.X)] no las protege solo: cada
/// tool que escribe llama a esto con la misma política que usaría el controlador equivalente.</summary>
public static class PolicyGuard
{
    public static async Task RequireAsync(IAuthorizationService authz, ClaimsPrincipal user, string policy)
    {
        var result = await authz.AuthorizeAsync(user, policy);
        if (!result.Succeeded) throw new McpException($"No tenés permiso para esto (falta la política '{policy}').");
    }
}
