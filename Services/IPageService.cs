using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface IPageService
{
    Task<IReadOnlyList<PageSummary>> ListAsync();
    Task<Result<PageDto>> GetAsync(Guid id);
    Task<Result<PageDto>> CreateAsync(PageCreateInput input, string userId);
    Task<Result> UpdateAsync(Guid id, PageUpdateInput input);
    Task<Result> MoveAsync(Guid id, PageMoveInput input);
    Task<Result> DeleteAsync(Guid id);
}
