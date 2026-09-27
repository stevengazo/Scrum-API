using Scrum.Api.Models;

namespace Scrum.Api.Services;

public record RegisteredAccount(AppUser User, string TenantName, string[] Roles);

/// <summary>Lógica de cuentas compartida entre AuthController (REST) y Mcp/Tools/AuthTools.cs (MCP), para no
/// reimplementarla dos veces con el riesgo de que se desincronicen.</summary>
public interface IAccountService
{
    /// <summary>Sin Slug crea una organización (el usuario queda Admin); con Slug se une a una existente (Viewer,
    /// salvo que sea el primer usuario de ese tenant).</summary>
    Task<Result<RegisteredAccount>> RegisterAsync(string email, string password, string displayName, string? orgName, string? slug, bool allowRegistration);
    Task<Result<AppUser>> LoginAsync(string email, string password);
    Task<(string[] Roles, string TenantName)> IdentityAsync(AppUser user);
}
