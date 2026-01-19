using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Services;

public interface IMlJobService
{
    Task EnqueueMlJobAsync(Guid assetId, MlJobType jobType, CancellationToken cancellationToken = default);
    Task<List<AssetMlJob>> GetPendingJobsAsync(CancellationToken cancellationToken = default);
}
