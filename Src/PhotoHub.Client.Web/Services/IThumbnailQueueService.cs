using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

public interface IThumbnailQueueService
{
    IAsyncEnumerable<ThumbnailProgressUpdate> GenerateThumbnailsAsync(
        bool regenerateAll = false,
        CancellationToken cancellationToken = default);
}
