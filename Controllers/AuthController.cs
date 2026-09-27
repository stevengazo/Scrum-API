using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

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

/// <summary>La lógica de cuentas vive en IAccountService (Services/AccountService.cs), compartida con las tools
/// MCP de Mcp/Tools/AuthTools.cs — este controlador solo traduce a HTTP.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, TokenService tokens, IConfiguration config, IAccountService accounts) : ControllerBase
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
        var allow = config.GetValue("Auth:AllowRegistration", true);
        var result = await accounts.RegisterAsync(input.Email, input.Password, input.DisplayName, input.OrgName, input.Slug, allow);
        return result.Error is { } e ? this.ToFailure(e)
            : new AuthResponse(tokens.Create(result.Value!.User, result.Value!.Roles),
                new UserDto(result.Value!.User.Id, result.Value!.User.Email!, result.Value!.User.DisplayName, result.Value!.User.Color, result.Value!.Roles, result.Value!.TenantName));
    }

    /// <summary>Autentica por correo y contraseña, en cualquier organización.</summary>
    /// <response code="200">Credenciales válidas; devuelve el JWT (con el tenant en el claim "tid") y el usuario.</response>
    /// <response code="401">Correo o contraseña incorrectos, o cuenta bloqueada por intentos fallidos.</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginInput input)
    {
        var result = await accounts.LoginAsync(input.Email, input.Password);
        if (result.Error is not null) return Unauthorized(result.Error.Message);
        return await Respond(result.Value!);
    }

    /// <summary>Usuario autenticado, con su organización actual.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await users.FindByIdAsync(User.UserId());
        if (user is null) return Unauthorized();
        var (roles, tenantName) = await accounts.IdentityAsync(user);
        return new UserDto(user.Id, user.Email!, user.DisplayName, user.Color, roles, tenantName);
    }

    private async Task<AuthResponse> Respond(AppUser user)
    {
        var (roles, tenantName) = await accounts.IdentityAsync(user);
        return new AuthResponse(tokens.Create(user, roles), new UserDto(user.Id, user.Email!, user.DisplayName, user.Color, roles, tenantName));
    }
}
