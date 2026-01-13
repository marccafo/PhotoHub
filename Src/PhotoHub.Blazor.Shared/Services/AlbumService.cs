using System.Net.Http.Json;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class AlbumService : IAlbumService
{
    private readonly HttpClient _httpClient;

    public AlbumService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AlbumItem>> GetAlbumsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<AlbumItem>>("/api/albums");
            return response ?? new List<AlbumItem>();
        }
        catch
        {
            return new List<AlbumItem>();
        }
    }

    public async Task<AlbumItem?> GetAlbumByIdAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<AlbumItem>($"/api/albums/{id}");
            return response;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TimelineItem>> GetAlbumAssetsAsync(int albumId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<TimelineItem>>($"/api/albums/{albumId}/assets");
            return response ?? new List<TimelineItem>();
        }
        catch
        {
            return new List<TimelineItem>();
        }
    }

    public async Task<AlbumItem?> CreateAlbumAsync(string name, string? description)
    {
        try
        {
            var request = new { Name = name, Description = description };
            var response = await _httpClient.PostAsJsonAsync("/api/albums", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AlbumItem>();
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateAlbumAsync(int id, string name, string? description)
    {
        try
        {
            var request = new { Name = name, Description = description };
            var response = await _httpClient.PutAsJsonAsync($"/api/albums/{id}", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteAlbumAsync(int id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/albums/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> AddAssetToAlbumAsync(int albumId, int assetId)
    {
        try
        {
            var request = new { AssetId = assetId };
            var response = await _httpClient.PostAsJsonAsync($"/api/albums/{albumId}/assets", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveAssetFromAlbumAsync(int albumId, int assetId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/albums/{albumId}/assets/{assetId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetAlbumCoverAsync(int albumId, int assetId)
    {
        try
        {
            var request = new { AssetId = assetId };
            var response = await _httpClient.PutAsJsonAsync($"/api/albums/{albumId}/cover", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
