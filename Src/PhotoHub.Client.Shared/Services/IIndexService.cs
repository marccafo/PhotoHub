using PhotoHub.Client.Shared.Models;

namespace PhotoHub.Client.Shared.Services;

public interface IIndexService
{
    IAsyncEnumerable<IndexProgressUpdate> IndexDirectoryAsync(CancellationToken cancellationToken = default);
}
