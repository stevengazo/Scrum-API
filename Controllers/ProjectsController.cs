using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

/// <param name="Key">Prefijo de las claves de historia (2-8 caracteres, A-Z/0-9, empieza con letra). Ej.: AGL.</param>
public record ProjectCreateInput(string Key, string Name, string? Description, string? Color);
public record ProjectUpdateInput(string Name, string? Description, string? Color, bool Archived);
public record ProjectDto(int Id, string Key, string Name, string? Description, string Color, bool Archived, int StoryCount, int DoneCount);

/// <summary>Proyectos: el contenedor de sprints, historias y páginas.</summary>
[ApiController]
[Route("api/projects")]
[Produces("application/json")]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>Lista los proyectos con su avance (historias totales y terminadas).</summary>
    [HttpGet]
    public async Task<IEnumerable<ProjectDto>> List() => await projects.ListAsync();

    /// <summary>Obtiene un proyecto por id.</summary>
    /// <response code="404">El proyecto no existe.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Get(int id) => this.ToOk(await projects.GetAsync(id));

    /// <summary>Crea un proyecto. Requiere rol Admin, ScrumMaster o ProductOwner.</summary>
    /// <response code="400">Clave o nombre inválidos.</response>
    /// <response code="409">Ya existe un proyecto con esa clave.</response>
    [HttpPost]
    [Authorize(Policies.ManageProjects)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectDto>> Create(ProjectCreateInput input)
    {
        var result = await projects.CreateAsync(input, User.TenantId());
        return result.Error is { } e ? this.ToFailure(e) : CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Actualiza nombre, descripción, color y estado de archivado.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policies.ManageProjects)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ProjectUpdateInput input) => this.ToNoContent(await projects.UpdateAsync(id, input));

    /// <summary>Elimina el proyecto y todo su contenido. Solo Admin.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policies.ManageUsers)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id) => this.ToNoContent(await projects.DeleteAsync(id));
}
