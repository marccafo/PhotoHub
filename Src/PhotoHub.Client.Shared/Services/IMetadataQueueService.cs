using PhotoHub.Client.Shared.Models;

namespace PhotoHub.Client.Shared.Services;

public interface IMetadataQueueService
{
    IAsyncEnumerable<MetadataProgressUpdate> ExtractMetadataAsync(
        bool overwriteAll = false,
        CancellationToken cancellationToken = default);
}
