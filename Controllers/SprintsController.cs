using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

public record SprintInput(string Name, string? Goal, DateOnly StartDate, DateOnly EndDate, int Capacity);

/// <summary>Sprints de un proyecto. Ciclo de vida: Planned → Active → Completed.</summary>
[ApiController]
[Route("api/projects/{projectId:int}/sprints")]
[Produces("application/json")]
public class SprintsController(ISprintService sprints) : ControllerBase
{
    /// <summary>Lista los sprints del proyecto por fecha de inicio.</summary>
    [HttpGet]
    public async Task<IEnumerable<Sprint>> List(int projectId) => await sprints.ListAsync(projectId);

    /// <summary>Crea un sprint planificado. Requiere Admin o ScrumMaster.</summary>
    /// <response code="400">Nombre vacío, fechas incoherentes o capacidad negativa.</response>
    [HttpPost]
    [Authorize(Policies.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Sprint>> Create(int projectId, SprintInput input)
    {
        var result = await sprints.CreateAsync(projectId, input);
        return result.Error is { } e ? this.ToFailure(e) : Created($"api/projects/{projectId}/sprints/{result.Value!.Id}", result.Value);
    }

    /// <summary>Actualiza los datos de un sprint.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policies.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int projectId, int id, SprintInput input) => this.ToNoContent(await sprints.UpdateAsync(projectId, id, input));

    /// <summary>Inicia un sprint planificado. Solo puede haber uno activo por proyecto.</summary>
    [HttpPost("{id:int}/start")]
    [Authorize(Policies.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(int projectId, int id) => this.ToNoContent(await sprints.StartAsync(projectId, id));

    /// <summary>Cierra el sprint: lo no terminado vuelve al backlog y compite de nuevo por prioridad.</summary>
    /// <returns>Cuántas historias volvieron al backlog.</returns>
    [HttpPost("{id:int}/complete")]
    [Authorize(Policies.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> Complete(int projectId, int id)
    {
        var result = await sprints.CompleteAsync(projectId, id);
        return result.Error is { } e ? this.ToFailure(e) : new { returnedToBacklog = result.Value };
    }

    /// <summary>Elimina un sprint no cerrado; sus historias vuelven al backlog.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policies.ManageSprints)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int projectId, int id) => this.ToNoContent(await sprints.DeleteAsync(projectId, id));
}
