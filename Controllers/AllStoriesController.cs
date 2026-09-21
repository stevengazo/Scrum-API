using Microsoft.AspNetCore.Mvc;
using Scrum.Api.Auth;
using Scrum.Api.Services;

namespace Scrum.Api.Controllers;

/// <summary>Vista transversal: historias de todos los proyectos.</summary>
[ApiController]
[Route("api/stories")]
[Produces("application/json")]
public class AllStoriesController(IStoryService stories) : ControllerBase
{
    /// <summary>Lista las historias de todos los proyectos.</summary>
    /// <param name="assignee">"me" para las del usuario autenticado, o el id de un usuario. Vacío = todas.</param>
    [HttpGet]
    public async Task<List<StoryDto>> List(string? assignee = null) =>
        await stories.ListAllAsync(assignee == "me" ? User.UserId() : string.IsNullOrEmpty(assignee) ? null : assignee);
}
