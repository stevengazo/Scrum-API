using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scrum.Api.Auth;
using Scrum.Api.Data;

namespace Scrum.Api.Controllers;

public record TenantNameDto(string Name);
public record TenantDto(string Name, string Slug);

/// <summary>Solo lo mínimo para la página de registro (validar un slug antes de mostrar el formulario) y para el
/// enlace de invitación en Usuarios y roles.</summary>
[ApiController]
[Route("api/tenants")]
[Produces("application/json")]
public class TenantsController(ScrumDbContext db) : ControllerBase
{
    /// <summary>Nombre de la organización a la que invita ese enlace, o 404 si el slug no existe.</summary>
    /// <response code="200">El slug existe; devuelve el nombre de la organización.</response>
    /// <response code="404">Ninguna organización usa ese slug.</response>
    [AllowAnonymous]
    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantNameDto>> BySlug(string slug)
    {
        var name = await db.Tenants.Where(t => t.Slug == slug.Trim().ToLowerInvariant()).Select(t => t.Name).FirstOrDefaultAsync();
        return name is null ? NotFound() : new TenantNameDto(name);
    }

    /// <summary>Organización del usuario autenticado, para armar su enlace de invitación.</summary>
    [HttpGet("current")]
    public async Task<TenantDto> Current()
    {
        var tenantId = User.TenantId();
        return await db.Tenants.Where(t => t.Id == tenantId).Select(t => new TenantDto(t.Name, t.Slug)).FirstAsync();
    }
}
