using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class IndexService : IIndexService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IndexService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IAsyncEnumerable<IndexProgressUpdate> IndexDirectoryAsync(
        CancellationToken cancellationToken = default)
    {
        var url = "/api/assets/index/stream";
        return _httpClient.GetFromJsonAsAsyncEnumerable<IndexProgressUpdate>(url, _jsonOptions, cancellationToken)!;
    }
}
