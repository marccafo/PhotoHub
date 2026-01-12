using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IIndexService
{
    IAsyncEnumerable<IndexProgressUpdate> IndexDirectoryAsync(CancellationToken cancellationToken = default);
}
