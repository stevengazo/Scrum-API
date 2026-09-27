using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync();
    Task<Result<ProjectDto>> GetAsync(int id);
    Task<Result<ProjectDto>> CreateAsync(ProjectCreateInput input, int tenantId);
    Task<Result> UpdateAsync(int id, ProjectUpdateInput input);
    Task<Result> DeleteAsync(int id);
}
