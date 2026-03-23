using System.Net.Http.Json;
using System.Text.Json;
using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

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
