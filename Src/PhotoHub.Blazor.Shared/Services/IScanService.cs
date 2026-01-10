using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IScanService
{
    Task<ScanResult> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default);
}
