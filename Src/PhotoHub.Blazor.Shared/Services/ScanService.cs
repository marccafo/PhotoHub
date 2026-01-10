using System.Net.Http.Json;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class ScanService : IScanService
{
    private readonly HttpClient _httpClient;

    public ScanService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ScanResult> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ScanResult>(
            $"/api/assets/scan?directoryPath={Uri.EscapeDataString(directoryPath)}",
            cancellationToken);
        
        return response ?? new ScanResult();
    }
}
