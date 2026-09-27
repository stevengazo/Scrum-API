using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface ICommentService
{
    Task<Result<List<CommentDto>>> ListAsync(int projectId, int storyId);
    Task<Result<CommentDto>> CreateAsync(int projectId, int storyId, string authorId, string text);
    /// <summary>El autor borra el suyo; un moderador (Admin/ScrumMaster) borra cualquiera.</summary>
    Task<Result> DeleteAsync(int projectId, int storyId, int id, string actorId, bool isModerator);
}
