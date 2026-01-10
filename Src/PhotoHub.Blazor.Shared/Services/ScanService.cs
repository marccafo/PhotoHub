using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class ScanService : IScanService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ScanService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IAsyncEnumerable<ScanProgressUpdate> ScanDirectoryAsync(
        string directoryPath, 
        CancellationToken cancellationToken = default)
    {
        var url = $"/api/assets/scan/stream?directoryPath={Uri.EscapeDataString(directoryPath)}";
        return _httpClient.GetFromJsonAsAsyncEnumerable<ScanProgressUpdate>(url, _jsonOptions, cancellationToken)!;
    }
}
