using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

public interface IMetadataQueueService
{
    IAsyncEnumerable<MetadataProgressUpdate> ExtractMetadataAsync(
        bool overwriteAll = false,
        CancellationToken cancellationToken = default);
}
