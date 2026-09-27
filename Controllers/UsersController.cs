using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Controllers;

public record CreateUserInput(string Email, string Password, string DisplayName, string Role);
public record UpdateUserInput(string DisplayName, string Role);

[ApiController]
[Route("api/users")]
public class UsersController(UserManager<AppUser> users, ScrumDbContext db) : ControllerBase
{
    /// <summary>Lista para selectores (responsables). Disponible para cualquier usuario autenticado.</summary>
    [HttpGet]
    public async Task<IEnumerable<UserDto>> List()
    {
        var roleByUser = await (from ur in db.UserRoles
                                join r in db.Roles on ur.RoleId equals r.Id
                                select new { ur.UserId, r.Name }).ToListAsync();
        var list = await users.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();
        // Todos son del mismo tenant (users.Users ya viene acotado por el filtro global): una sola consulta basta.
        var tenantName = await db.Tenants.Where(t => t.Id == User.TenantId()).Select(t => t.Name).FirstAsync();
        return list.Select(u => new UserDto(u.Id, u.Email!, u.DisplayName, u.Color,
            [.. roleByUser.Where(x => x.UserId == u.Id).Select(x => x.Name!)], tenantName));
    }

    [HttpPost]
    [Authorize(Policies.ManageUsers)]
    public async Task<ActionResult<UserDto>> Create(CreateUserInput input)
    {
        if (!Roles.All.Contains(input.Role)) return BadRequest("Rol inválido.");
        var email = input.Email.Trim();
        // IgnoreQueryFilters: el chequeo de unicidad de Identity solo ve el tenant actual por el filtro global; el
        // correo es único en toda la instalación.
        var normalizedEmail = users.NormalizeEmail(email);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            return BadRequest("Ya existe una cuenta con ese correo.");
        var user = new AppUser { TenantId = User.TenantId(), UserName = email, Email = email, DisplayName = input.DisplayName.Trim(), Color = AuthExtensions.ColorFor(email) };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded) return BadRequest(string.Join(" ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(user, input.Role);
        var tenantName = await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Name).FirstAsync();
        return new UserDto(user.Id, email, user.DisplayName, user.Color, [input.Role], tenantName);
    }

    [HttpPut("{id}")]
    [Authorize(Policies.ManageUsers)]
    public async Task<IActionResult> Update(string id, UpdateUserInput input)
    {
        if (!Roles.All.Contains(input.Role)) return BadRequest("Rol inválido.");
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();

        var current = await users.GetRolesAsync(user);
        if (current.Contains(Roles.Admin) && input.Role != Roles.Admin && await OnlyAdmin(user))
            return BadRequest("Debe existir al menos un Admin.");

        user.DisplayName = input.DisplayName.Trim();
        await users.UpdateAsync(user);
        await users.RemoveFromRolesAsync(user, current);
        await users.AddToRoleAsync(user, input.Role);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policies.ManageUsers)]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == User.UserId()) return BadRequest("No puedes eliminarte a ti mismo.");
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await users.IsInRoleAsync(user, Roles.Admin) && await OnlyAdmin(user))
            return BadRequest("Debe existir al menos un Admin.");
        await db.Stories.Where(s => s.AssigneeId == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.AssigneeId, (string?)null));
        await users.DeleteAsync(user);
        return NoContent();
    }

    private async Task<bool> OnlyAdmin(AppUser user) =>
        (await users.GetUsersInRoleAsync(Roles.Admin)).All(u => u.Id == user.Id);
}
