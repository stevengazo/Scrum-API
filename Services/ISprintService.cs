using Scrum.Api.Controllers;
using Scrum.Api.Models;

namespace Scrum.Api.Services;

public interface ISprintService
{
    Task<IReadOnlyList<Sprint>> ListAsync(int projectId);
    Task<Result<Sprint>> CreateAsync(int projectId, SprintInput input);
    Task<Result> UpdateAsync(int projectId, int id, SprintInput input);
    Task<Result> StartAsync(int projectId, int id);
    /// <summary>Cierra el sprint y devuelve cuántas historias volvieron al backlog. Notifica a los responsables (menos a actorId).</summary>
    Task<Result<int>> CompleteAsync(int projectId, int id, string actorId);
    Task<Result> DeleteAsync(int projectId, int id);
}
