using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Models;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

public record PageSummary(Guid Id, int? ProjectId, Guid? ParentId, string Title, string? Icon, double Order, DateTime UpdatedAt, PageKind Kind);
public record PageDto(Guid Id, int? ProjectId, Guid? ParentId, string Title, string? Icon, string? Content, double Order, DateTime UpdatedAt, PageKind Kind);
/// <param name="Kind">Page (por defecto) o Sheet. Una hoja no tiene padre ni hijos.</param>
public record PageCreateInput(string? Title, string? Icon, Guid? ParentId, int? ProjectId, PageKind? Kind = null);
/// <param name="Content">JSON serializado del EditorState de Lexical.</param>
public record PageUpdateInput(string Title, string? Icon, string? Content);
public record PageMoveInput(Guid? ParentId, double Order);

/// <summary>Páginas de documentación (editor de bloques), organizadas en árbol.</summary>
[ApiController]
[Route("api/pages")]
[Produces("application/json")]
public class PagesController(IPageService pages) : ControllerBase
{
    /// <summary>Solo metadatos: el cliente arma el árbol. El contenido se pide página a página.</summary>
    [HttpGet]
    public async Task<IEnumerable<PageSummary>> List() => await pages.ListAsync();

    /// <summary>Obtiene una página con su contenido.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageDto>> Get(Guid id) => this.ToOk(await pages.GetAsync(id));

    /// <summary>Crea una página, opcionalmente hija de otra o de un proyecto.</summary>
    [HttpPost]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PageDto>> Create(PageCreateInput input)
    {
        var result = await pages.CreateAsync(input, User.UserId());
        return result.Error is { } e ? this.ToFailure(e) : CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Guarda título, icono y contenido.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, PageUpdateInput input) => this.ToNoContent(await pages.UpdateAsync(id, input));

    /// <summary>Cambia el padre y la posición de una página. Rechaza ciclos.</summary>
    [HttpPost("{id:guid}/move")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(Guid id, PageMoveInput input) => this.ToNoContent(await pages.MoveAsync(id, input));

    /// <summary>Elimina la página y todos sus descendientes.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policies.Contribute)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id) => this.ToNoContent(await pages.DeleteAsync(id));
}
