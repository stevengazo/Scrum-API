using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface ITodoService
{
    Task<List<TodoDto>> ListAsync(string userId);
    Task<Result<TodoDto>> CreateAsync(string userId, TodoCreateInput input);
    Task<Result> UpdateAsync(string userId, int id, TodoUpdateInput input);
    Task<Result<TodoDto>> AddPomodoroAsync(string userId, int id);
    Task<Result> DeleteAsync(string userId, int id);
    Task<int> ClearDoneAsync(string userId);
}
