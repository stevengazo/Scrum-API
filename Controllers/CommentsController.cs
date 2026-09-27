using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

public record CommentDto(int Id, int StoryId, string AuthorId, string AuthorName, string AuthorColor, string Text, DateTime CreatedAt, DateTime? EditedAt);
/// <param name="Text">Máximo 2000 caracteres. No puede estar vacío.</param>
public record CommentInput(string Text);

/// <summary>Hilo de comentarios de una historia (plano, sin respuestas anidadas).</summary>
[ApiController]
[Route("api/projects/{projectId:int}/stories/{storyId:int}/comments")]
[Produces("application/json")]
public class CommentsController(ICommentService comments) : ControllerBase
{
    /// <summary>Lista el hilo completo, del más antiguo al más nuevo.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CommentDto>>> List(int projectId, int storyId) => this.ToOk(await comments.ListAsync(projectId, storyId));

    /// <summary>Agrega un comentario. Notifica al responsable y a quien ya haya participado en el hilo.</summary>
    [HttpPost]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> Create(int projectId, int storyId, CommentInput input)
    {
        var result = await comments.CreateAsync(projectId, storyId, User.UserId(), input.Text);
        return result.Error is { } e ? this.ToFailure(e) : Created($"api/projects/{projectId}/stories/{storyId}/comments/{result.Value!.Id}", result.Value);
    }

    /// <summary>Borra un comentario: el autor el suyo, Admin/ScrumMaster cualquiera.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int projectId, int storyId, int id)
    {
        var isModerator = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.ScrumMaster);
        return this.ToNoContent(await comments.DeleteAsync(projectId, storyId, id, User.UserId(), isModerator));
    }
}
