using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface IStoryService
{
    Task<Result<List<StoryDto>>> ListAsync(int projectId);
    /// <summary>Historias de todos los proyectos, opcionalmente solo las de un responsable.</summary>
    Task<List<StoryDto>> ListAllAsync(string? assigneeId);
    Task<Result<StoryDto>> CreateAsync(int projectId, StoryInput input);
    Task<Result> UpdateAsync(int projectId, int id, StoryInput input);
    Task<Result> MoveAsync(int projectId, int id, MoveInput input);
    Task<Result> DeleteAsync(int projectId, int id);
}
