using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IScanService
{
    IAsyncEnumerable<ScanProgressUpdate> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
