using PhotoHub.Server.Api.Shared.Models;

namespace PhotoHub.Server.Api.Shared.Services;

public interface IMlJobService
{
    Task EnqueueMlJobAsync(Guid assetId, MlJobType jobType, CancellationToken cancellationToken = default);
    Task<List<AssetMlJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default);
}
