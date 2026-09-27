using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Data;

namespace Scrum.Api.Mcp.Tools;

[McpServerToolType]
public class TenantTools(ScrumDbContext db, McpSessionStore sessions, IHttpContextAccessor accessor)
{
    [McpServerTool, Description("Nombre de la organización de un enlace de invitación, antes de unirse a ella con register_organization. No requiere sesión.")]
    public async Task<TenantNameDto> GetTenantBySlug(McpServer server, [Description("Slug del enlace de invitación.")] string slug)
    {
        var name = await db.Tenants.Where(t => t.Slug == slug.Trim().ToLowerInvariant()).Select(t => t.Name).FirstOrDefaultAsync();
        return name is null ? throw new McpException("Ese enlace de invitación no es válido.") : new TenantNameDto(name);
    }

    [McpServerTool, Description("Organización de la sesión actual y su slug de invitación.")]
    public async Task<TenantDto> GetCurrentTenant(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        var tenantId = user.TenantId();
        return await db.Tenants.Where(t => t.Id == tenantId).Select(t => new TenantDto(t.Name, t.Slug)).FirstAsync();
    }
}
