using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Scrum.Api.Auth;
using Scrum.Api.Controllers;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Mcp.Tools;

/// <summary>Mismas reglas que UsersController: List es para cualquier autenticado (selectores de responsable);
/// Create/Update requieren la política ManageUsers (Admin).</summary>
[McpServerToolType]
public class UserTools(UserManager<AppUser> users, ScrumDbContext db, McpSessionStore sessions, IHttpContextAccessor accessor, IAuthorizationService authz)
{
    [McpServerTool, Description("Lista los usuarios de la organización actual, con su rol.")]
    public async Task<List<UserDto>> ListUsers(McpServer server)
    {
        var principal = PrincipalScope.Require(server, sessions, accessor);
        var roleByUser = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name }).ToListAsync();
        var list = await users.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();
        var tenantName = await db.Tenants.Where(t => t.Id == principal.TenantId()).Select(t => t.Name).FirstAsync();
        return [.. list.Select(u => new UserDto(u.Id, u.Email!, u.DisplayName, u.Color, [.. roleByUser.Where(x => x.UserId == u.Id).Select(x => x.Name!)], tenantName))];
    }

    [McpServerTool, Description("Crea un usuario en la organización actual con un rol dado. Requiere Admin.")]
    public async Task<UserDto> CreateUser(McpServer server,
        [Description("Correo del nuevo usuario.")] string email,
        [Description("Contraseña inicial, mínimo 8 caracteres.")] string password,
        [Description("Nombre visible.")] string displayName,
        [Description("Admin, ScrumMaster, ProductOwner, Developer o Viewer.")] string role)
    {
        var principal = await Require(server);
        if (!Roles.All.Contains(role)) throw new McpException("Rol inválido.");

        var trimmedEmail = email.Trim();
        var normalizedEmail = users.NormalizeEmail(trimmedEmail);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            throw new McpException("Ya existe una cuenta con ese correo.");

        var user = new AppUser { TenantId = principal.TenantId(), UserName = trimmedEmail, Email = trimmedEmail, DisplayName = displayName.Trim(), Color = AuthExtensions.ColorFor(trimmedEmail) };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new McpException(string.Join(" ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, role);

        var tenantName = await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Name).FirstAsync();
        return new UserDto(user.Id, trimmedEmail, user.DisplayName, user.Color, [role], tenantName);
    }

    [McpServerTool, Description("Cambia el nombre y/o el rol de un usuario de la organización actual. Requiere Admin. No permite dejar la organización sin ningún Admin.")]
    public async Task<string> UpdateUserRole(McpServer server,
        [Description("Id del usuario.")] string userId,
        [Description("Nuevo nombre visible.")] string displayName,
        [Description("Admin, ScrumMaster, ProductOwner, Developer o Viewer.")] string role)
    {
        await Require(server);
        if (!Roles.All.Contains(role)) throw new McpException("Rol inválido.");
        var user = await users.FindByIdAsync(userId) ?? throw new McpException("No encontrado.");

        var current = await users.GetRolesAsync(user);
        if (current.Contains(Roles.Admin) && role != Roles.Admin && (await users.GetUsersInRoleAsync(Roles.Admin)).All(u => u.Id == user.Id))
            throw new McpException("Debe existir al menos un Admin.");

        user.DisplayName = displayName.Trim();
        await users.UpdateAsync(user);
        await users.RemoveFromRolesAsync(user, current);
        await users.AddToRoleAsync(user, role);
        return "Actualizado.";
    }

    private async Task<System.Security.Claims.ClaimsPrincipal> Require(McpServer server)
    {
        var user = PrincipalScope.Require(server, sessions, accessor);
        await PolicyGuard.RequireAsync(authz, user, Policies.ManageUsers);
        return user;
    }
}
