using Scrum.Api.Controllers;

namespace Scrum.Api.Services;

public interface IMetricsService
{
    Task<Result<SummaryDto>> SummaryAsync(int projectId);
    Task<Result<List<VelocityPointDto>>> VelocityAsync(int projectId);
    Task<Result<List<BurndownPointDto>>> BurndownAsync(int projectId, int sprintId);
}
