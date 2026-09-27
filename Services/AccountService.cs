using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public class AccountService(UserManager<AppUser> users, ScrumDbContext db) : IAccountService
{
    public async Task<Result<RegisteredAccount>> RegisterAsync(string email, string password, string displayName, string? orgName, string? slug, bool allowRegistration)
    {
        if (!allowRegistration) return Error.Invalid("El registro está cerrado.");
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(displayName))
            return Error.Invalid("Email y nombre son obligatorios.");

        bool isFirstInTenant;
        Tenant tenant;
        if (string.IsNullOrWhiteSpace(slug))
        {
            if (string.IsNullOrWhiteSpace(orgName)) return Error.Invalid("El nombre de la organización es obligatorio.");
            tenant = new Tenant { Name = orgName.Trim(), Slug = await UniqueSlug(orgName) };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            isFirstInTenant = true; // siempre: la organización recién nace
        }
        else
        {
            var normalizedSlug = slug.Trim().ToLowerInvariant();
            var found = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == normalizedSlug);
            if (found is null) return Error.NotFound();
            tenant = found;
            // IgnoreQueryFilters: el filtro global de AppUser exige un tenant ya conocido, y acá todavía no lo hay.
            isFirstInTenant = !await db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == tenant.Id);
        }

        var trimmedEmail = email.Trim();
        var normalizedEmail = users.NormalizeEmail(trimmedEmail);
        // El chequeo de unicidad de Identity (dentro de CreateAsync) no ve otros tenants por el filtro global; este es el real.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            return Error.Invalid("Ya existe una cuenta con ese correo.");

        var user = new AppUser
        {
            TenantId = tenant.Id, UserName = trimmedEmail, Email = trimmedEmail,
            DisplayName = displayName.Trim(), Color = AuthExtensions.ColorFor(trimmedEmail),
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) return Error.Invalid(string.Join(" ", created.Errors.Select(e => e.Description)));

        var role = isFirstInTenant ? Roles.Admin : Roles.Viewer;
        await users.AddToRoleAsync(user, role);
        return new RegisteredAccount(user, tenant.Name, [role]);
    }

    public async Task<Result<AppUser>> LoginAsync(string email, string password)
    {
        // IgnoreQueryFilters: sin sesión todavía no hay tenant conocido; el correo es único en toda la instalación.
        var normalizedEmail = users.NormalizeEmail(email.Trim());
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null || await users.IsLockedOutAsync(user)) return Error.Invalid("Credenciales inválidas.");

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return Error.Invalid("Credenciales inválidas.");
        }
        await users.ResetAccessFailedCountAsync(user);
        return user;
    }

    public async Task<(string[] Roles, string TenantName)> IdentityAsync(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var tenantName = await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Name).FirstAsync();
        return ([.. roles], tenantName);
    }

    private async Task<string> UniqueSlug(string name)
    {
        var baseSlug = AuthExtensions.SlugFor(name);
        var slug = baseSlug;
        for (var i = 2; await db.Tenants.AnyAsync(t => t.Slug == slug); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
