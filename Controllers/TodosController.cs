using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

/// <param name="Text">Texto libre. Si se indica StoryId y va vacío, se usa el título de la historia.</param>
/// <param name="StoryId">Historia a la que se enlaza el item, o null para una nota libre.</param>
public record TodoCreateInput(string? Text, int? StoryId);

/// <param name="Text">Nuevo texto del item.</param>
/// <param name="Done">Si el item está hecho.</param>
public record TodoUpdateInput(string Text, bool Done);

public record TodoDto(
    int Id, string Text, bool Done, int? StoryId, string? StoryKey, string? StoryTitle, int? ProjectId,
    int Pomodoros, double Order, DateTime CreatedAt, DateTime? CompletedAt);

/// <summary>Lista personal de tareas con pomodoros. Cada usuario solo accede a la suya.</summary>
[ApiController]
[Route("api/todos")]
[Produces("application/json")]
public class TodosController(ITodoService todos) : ControllerBase
{
    /// <summary>Items del usuario: pendientes primero, por orden de creación.</summary>
    [HttpGet]
    public async Task<List<TodoDto>> List() => await todos.ListAsync(User.UserId());

    /// <summary>Agrega una nota libre o una historia (StoryId).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TodoDto>> Create(TodoCreateInput input)
    {
        var result = await todos.CreateAsync(User.UserId(), input);
        return result.Error is { } e ? this.ToFailure(e) : Created($"api/todos/{result.Value!.Id}", result.Value);
    }

    /// <summary>Edita el texto o marca/desmarca como hecho.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, TodoUpdateInput input) => this.ToNoContent(await todos.UpdateAsync(User.UserId(), id, input));

    /// <summary>Suma un pomodoro completado al item.</summary>
    [HttpPost("{id:int}/pomodoro")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TodoDto>> AddPomodoro(int id) => this.ToOk(await todos.AddPomodoroAsync(User.UserId(), id));

    /// <summary>Elimina un item.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id) => this.ToNoContent(await todos.DeleteAsync(User.UserId(), id));

    /// <summary>Elimina todos los items hechos.</summary>
    /// <returns>Cuántos se eliminaron.</returns>
    [HttpDelete("completed")]
    public async Task<object> ClearDone() => new { removed = await todos.ClearDoneAsync(User.UserId()) };
}
