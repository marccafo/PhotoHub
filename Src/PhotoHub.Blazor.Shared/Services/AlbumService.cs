using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class AlbumService : IAlbumService
{
    private readonly HttpClient _httpClient;
    private readonly Func<Task<string?>>? _getTokenFunc;

    public AlbumService(HttpClient httpClient, Func<Task<string?>>? getTokenFunc = null)
    {
        _httpClient = httpClient;
        _getTokenFunc = getTokenFunc;
    }

    private async Task SetAuthHeaderAsync()
    {
        string? token = null;
        if (_getTokenFunc != null)
        {
            token = await _getTokenFunc();
        }

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private static void ThrowIfForbidden(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedAccessException("No tienes permisos suficientes para realizar esta acción.");
        }
    }

    public async Task<List<AlbumItem>> GetAlbumsAsync()
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetFromJsonAsync<List<AlbumItem>>("/api/albums");
            return response ?? new List<AlbumItem>();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/albums/{id}");
            ThrowIfForbidden(response);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AlbumItem>();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"/api/albums/{albumId}/assets");
            ThrowIfForbidden(response);
            if (!response.IsSuccessStatusCode)
            {
                return new List<TimelineItem>();
            }

            return await response.Content.ReadFromJsonAsync<List<TimelineItem>>() ?? new List<TimelineItem>();
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var request = new { Name = name, Description = description };
            var response = await _httpClient.PostAsJsonAsync("/api/albums", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AlbumItem>();
            }
            
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var request = new { Name = name, Description = description };
            var response = await _httpClient.PutAsJsonAsync($"/api/albums/{id}", request);
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/albums/{id}");
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> LeaveAlbumAsync(int albumId)
    {
        try
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.PostAsync($"/api/albums/{albumId}/leave", null);
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var request = new { AssetId = assetId };
            var response = await _httpClient.PostAsJsonAsync($"/api/albums/{albumId}/assets", request);
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var response = await _httpClient.DeleteAsync($"/api/albums/{albumId}/assets/{assetId}");
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
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
            await SetAuthHeaderAsync();
            var request = new { AssetId = assetId };
            var response = await _httpClient.PutAsJsonAsync($"/api/albums/{albumId}/cover", request);
            ThrowIfForbidden(response);
            return response.IsSuccessStatusCode;
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
