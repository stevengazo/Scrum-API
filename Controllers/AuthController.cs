using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Data;
using Scrum.Api.Models;

namespace Scrum.Api.Controllers;

/// <param name="Email">Único en toda la instalación, no solo dentro de la organización.</param>
/// <param name="Password">Mínimo 8 caracteres.</param>
/// <param name="DisplayName">Nombre visible del usuario.</param>
/// <param name="OrgName">Nombre de la organización nueva. Obligatorio si Slug es null (se crea una organización).</param>
/// <param name="Slug">Slug de una organización existente a la que unirse (viene del enlace de invitación). Si se
/// da, OrgName se ignora.</param>
public record RegisterInput(string Email, string Password, string DisplayName, string? OrgName, string? Slug);
public record LoginInput(string Email, string Password);
public record AuthResponse(string Token, UserDto User);

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, TokenService tokens, IConfiguration config, ScrumDbContext db) : ControllerBase
{
    /// <summary>Crea una organización nueva (sin Slug, el usuario queda Admin) o se une a una existente (con Slug,
    /// el usuario queda Viewer salvo que sea el primero de esa organización).</summary>
    /// <response code="200">Cuenta creada; devuelve el JWT y el usuario.</response>
    /// <response code="400">Faltan datos, el correo ya existe, o el registro está cerrado (Auth:AllowRegistration).</response>
    /// <response code="404">El Slug no corresponde a ninguna organización (enlace de invitación inválido).</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterInput input)
    {
        if (!config.GetValue("Auth:AllowRegistration", true)) return Forbid();
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.DisplayName))
            return BadRequest("Email y nombre son obligatorios.");

        bool isFirstInTenant;
        Tenant tenant;
        if (string.IsNullOrWhiteSpace(input.Slug))
        {
            if (string.IsNullOrWhiteSpace(input.OrgName)) return BadRequest("El nombre de la organización es obligatorio.");
            tenant = new Tenant { Name = input.OrgName.Trim(), Slug = await UniqueSlug(input.OrgName) };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            isFirstInTenant = true; // siempre: la organización recién nace
        }
        else
        {
            var slug = input.Slug.Trim().ToLowerInvariant();
            var found = await db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
            if (found is null) return NotFound("El enlace de invitación no es válido.");
            tenant = found;
            // IgnoreQueryFilters: el filtro global de AppUser exige el tenant del JWT, y este request es anónimo.
            isFirstInTenant = !await db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == tenant.Id);
        }

        var email = input.Email.Trim();
        var normalizedEmail = users.NormalizeEmail(email);
        // El primer chequeo de Identity (dentro de CreateAsync) no ve otros tenants por el filtro global; este es
        // el real.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedEmail == normalizedEmail))
            return BadRequest("Ya existe una cuenta con ese correo.");

        var user = new AppUser
        {
            TenantId = tenant.Id, UserName = email, Email = email,
            DisplayName = input.DisplayName.Trim(), Color = AuthExtensions.ColorFor(email),
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded) return BadRequest(string.Join(" ", created.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, isFirstInTenant ? Roles.Admin : Roles.Viewer);
        return await Respond(user, tenant.Name);
    }

    /// <summary>Autentica por correo y contraseña, en cualquier organización.</summary>
    /// <response code="200">Credenciales válidas; devuelve el JWT (con el tenant en el claim "tid") y el usuario.</response>
    /// <response code="401">Correo o contraseña incorrectos, o cuenta bloqueada por intentos fallidos.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginInput input)
    {
        // IgnoreQueryFilters: sin JWT todavía no hay tenant conocido; el correo es único en toda la instalación.
        var normalizedEmail = users.NormalizeEmail(input.Email.Trim());
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (user is null || await users.IsLockedOutAsync(user)) return Unauthorized("Credenciales inválidas.");

        if (!await users.CheckPasswordAsync(user, input.Password))
        {
            await users.AccessFailedAsync(user);
            return Unauthorized("Credenciales inválidas.");
        }
        await users.ResetAccessFailedCountAsync(user);
        return await Respond(user);
    }

    /// <summary>Usuario autenticado, con su organización actual.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await users.FindByIdAsync(User.UserId());
        return user is null ? Unauthorized() : await ToDto(user);
    }

    private async Task<AuthResponse> Respond(AppUser user, string? tenantName = null)
    {
        var roles = await users.GetRolesAsync(user);
        tenantName ??= await db.Tenants.Where(t => t.Id == user.TenantId).Select(t => t.Name).FirstAsync();
        return new AuthResponse(tokens.Create(user, roles), new UserDto(user.Id, user.Email!, user.DisplayName, user.Color, [.. roles], tenantName));
    }

    private async Task<UserDto> ToDto(AppUser u)
    {
        var tenantName = await db.Tenants.Where(t => t.Id == u.TenantId).Select(t => t.Name).FirstAsync();
        return new UserDto(u.Id, u.Email!, u.DisplayName, u.Color, [.. await users.GetRolesAsync(u)], tenantName);
    }

    private async Task<string> UniqueSlug(string name)
    {
        var baseSlug = AuthExtensions.SlugFor(name);
        var slug = baseSlug;
        for (var i = 2; await db.Tenants.AnyAsync(t => t.Slug == slug); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
