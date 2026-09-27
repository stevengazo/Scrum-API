using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

public record CriterionDto(int? Id, string Text, bool Done);

public record StoryDto(
    int Id, int ProjectId, int Number, string Key, string Title, string? Description,
    StoryType Type, StoryStatus Status, Priority Priority, int? Points, int? SprintId, string? AssigneeId,
    double Order, DateTime CreatedAt, DateTime? CompletedAt, List<CriterionDto> Criteria);

/// <param name="Points">Escala Fibonacci (1, 2, 3, 5, 8, 13, 21) o null si no está estimada.</param>
public record StoryInput(
    string Title, string? Description, StoryType Type, StoryStatus Status, Priority Priority,
    int? Points, int? SprintId, string? AssigneeId, List<CriterionDto>? Criteria);

/// <summary>Estado destino completo tras arrastrar una tarjeta.</summary>
public record MoveInput(StoryStatus Status, int? SprintId, double Order);

/// <summary>Historias de usuario de un proyecto (backlog y tablero).</summary>
[ApiController]
[Route("api/projects/{projectId:int}/stories")]
[Produces("application/json")]
public class StoriesController(IStoryService stories) : ControllerBase
{
    /// <summary>Lista las historias del proyecto ordenadas por posición.</summary>
    /// <response code="404">El proyecto no existe.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<StoryDto>>> List(int projectId) => this.ToOk(await stories.ListAsync(projectId));

    /// <summary>Crea una historia al final de su columna. Requiere rol distinto de Viewer.</summary>
    [HttpPost]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StoryDto>> Create(int projectId, StoryInput input)
    {
        var result = await stories.CreateAsync(projectId, input, User.UserId());
        return result.Error is { } e ? this.ToFailure(e) : Created($"api/projects/{projectId}/stories/{result.Value!.Id}", result.Value);
    }

    /// <summary>Actualiza una historia y sincroniza sus criterios de aceptación.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int projectId, int id, StoryInput input) => this.ToNoContent(await stories.UpdateAsync(projectId, id, input, User.UserId()));

    /// <summary>Mueve una historia a otro estado, sprint o posición.</summary>
    [HttpPost("{id:int}/move")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(int projectId, int id, MoveInput input) => this.ToNoContent(await stories.MoveAsync(projectId, id, input));

    /// <summary>Elimina una historia.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int projectId, int id) => this.ToNoContent(await stories.DeleteAsync(projectId, id));
}
