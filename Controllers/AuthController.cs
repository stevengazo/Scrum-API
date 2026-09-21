using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Models;

namespace Scrum.Api.Controllers;

public record RegisterInput(string Email, string Password, string DisplayName);
public record LoginInput(string Email, string Password);
public record AuthResponse(string Token, UserDto User);

[ApiController]
[Route("api/auth")]
public class AuthController(UserManager<AppUser> users, TokenService tokens, IConfiguration config) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterInput input)
    {
        if (!config.GetValue("Auth:AllowRegistration", true)) return Forbid();
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.DisplayName))
            return BadRequest("Email y nombre son obligatorios.");

        // El primer usuario administra el sistema; el resto entra como Viewer hasta que un Admin lo promueva.
        var isFirst = !await users.Users.AnyAsync();
        var email = input.Email.Trim();
        var user = new AppUser
        {
            UserName = email, Email = email, DisplayName = input.DisplayName.Trim(), Color = AuthExtensions.ColorFor(email),
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded) return BadRequest(string.Join(" ", created.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, isFirst ? Roles.Admin : Roles.Viewer);
        return await Respond(user);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginInput input)
    {
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || await users.IsLockedOutAsync(user)) return Unauthorized("Credenciales inválidas.");

        if (!await users.CheckPasswordAsync(user, input.Password))
        {
            await users.AccessFailedAsync(user);
            return Unauthorized("Credenciales inválidas.");
        }
        await users.ResetAccessFailedCountAsync(user);
        return await Respond(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await users.FindByIdAsync(User.UserId());
        return user is null ? Unauthorized() : await ToDto(user);
    }

    private async Task<AuthResponse> Respond(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new AuthResponse(tokens.Create(user, roles), new UserDto(user.Id, user.Email!, user.DisplayName, user.Color, [.. roles]));
    }

    private async Task<UserDto> ToDto(AppUser u) =>
        new(u.Id, u.Email!, u.DisplayName, u.Color, [.. await users.GetRolesAsync(u)]);
}
