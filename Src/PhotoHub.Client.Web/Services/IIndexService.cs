using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

public interface IIndexService
{
    IAsyncEnumerable<IndexProgressUpdate> IndexDirectoryAsync(CancellationToken cancellationToken = default);
}
