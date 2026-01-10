using System.Net.Http.Json;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class FolderService : IFolderService
{
    private readonly HttpClient _httpClient;

    public FolderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<FolderItem>> GetFoldersAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<FolderItem>>("/api/folders");
            return response ?? new List<FolderItem>();
        }
        catch
        {
            return new List<FolderItem>();
        }
    }

    public async Task<FolderItem?> GetFolderByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<FolderItem>($"/api/folders/{id}");
            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<FolderItem>> GetFolderTreeAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<FolderItem>>("/api/folders/tree");
            return response ?? new List<FolderItem>();
        }
        catch
        {
            return new List<FolderItem>();
        }
    }

    public async Task<List<TimelineItem>> GetFolderAssetsAsync(int folderId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<TimelineItem>>($"/api/folders/{folderId}/assets");
            return response ?? new List<TimelineItem>();
        }
        catch
        {
            return new List<TimelineItem>();
        }
    }
}
