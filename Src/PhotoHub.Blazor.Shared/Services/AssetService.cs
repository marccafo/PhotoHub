using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public class AssetService : IAssetService
{
    private readonly HttpClient _httpClient;

    public AssetService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<TimelineItem>> GetTimelineAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<TimelineItem>>("/api/assets/timeline");
        return response ?? new List<TimelineItem>();
    }

    public async Task<TimelineItem?> GetAssetByIdAsync(int id)
    {
        var timeline = await GetTimelineAsync();
        return timeline.FirstOrDefault(a => a.Id == id);
    }

    public async Task<AssetDetail?> GetAssetDetailAsync(int id)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<AssetDetailResponse>($"/api/assets/{id}");
            return MapResponseToDetail(response);
        }
        catch
        {
            return null;
        }
    }

    public async Task<AssetDetail?> GetPendingAssetDetailAsync(string path)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<AssetDetailResponse>($"/api/assets/pending/detail?path={System.Net.WebUtility.UrlEncode(path)}");
            return MapResponseToDetail(response);
        }
        catch
        {
            return null;
        }
    }

    private AssetDetail? MapResponseToDetail(AssetDetailResponse? response)
    {
        if (response == null)
            return null;

        // Mapear de AssetDetailResponse a AssetDetail
        return new AssetDetail
        {
            Id = response.Id,
            FileName = response.FileName,
            FullPath = response.FullPath,
            FileSize = response.FileSize,
            CreatedDate = response.CreatedDate,
            ModifiedDate = response.ModifiedDate,
            Extension = response.Extension,
            ScannedAt = response.ScannedAt,
            Type = response.Type,
            Checksum = response.Checksum,
            HasExif = response.HasExif,
            HasThumbnails = response.HasThumbnails,
            FolderId = response.FolderId,
            FolderPath = response.FolderPath,
            Exif = response.Exif != null ? new ExifData
            {
                DateTaken = response.Exif.DateTaken,
                CameraMake = response.Exif.CameraMake,
                CameraModel = response.Exif.CameraModel,
                Width = response.Exif.Width,
                Height = response.Exif.Height,
                Orientation = response.Exif.Orientation,
                Latitude = response.Exif.Latitude,
                Longitude = response.Exif.Longitude,
                Altitude = response.Exif.Altitude,
                Iso = response.Exif.Iso,
                Aperture = response.Exif.Aperture,
                ShutterSpeed = response.Exif.ShutterSpeed,
                FocalLength = response.Exif.FocalLength,
                Description = response.Exif.Description,
                Keywords = response.Exif.Keywords,
                Software = response.Exif.Software
            } : null,
            Thumbnails = response.Thumbnails.Select(t => new ThumbnailInfo
            {
                Id = t.Id,
                Size = t.Size,
                Width = t.Width,
                Height = t.Height,
                AssetId = t.AssetId
            }).ToList(),
            Tags = response.Tags,
            SyncStatus = response.SyncStatus
        };
    }

    public async Task<List<TimelineItem>> GetAssetsByFolderAsync(int? folderId)
    {
        try
        {
            var url = folderId.HasValue 
                ? $"/api/folders/{folderId}/assets" 
                : "/api/assets/timeline";
            var response = await _httpClient.GetFromJsonAsync<List<TimelineItem>>(url);
            return response ?? new List<TimelineItem>();
        }
        catch
        {
            return new List<TimelineItem>();
        }
    }

    public async Task<UploadResponse?> UploadAssetAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        using var multipartContent = new MultipartFormDataContent();
        using var streamContent = new StreamContent(content);
        multipartContent.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync("/api/assets/upload", multipartContent, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<UploadResponse>(cancellationToken: cancellationToken);
        }

        return null;
    }

    public async Task<SyncAssetResponse?> SyncAssetAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/assets/sync?path={System.Net.WebUtility.UrlEncode(path)}", null, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SyncAssetResponse>(cancellationToken: cancellationToken);
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    public async IAsyncEnumerable<SyncProgressUpdate> SyncMultipleAssetsAsync(
        IEnumerable<string> paths, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pathsList = paths.ToList();
        var total = pathsList.Count;
        var processed = 0;
        var successful = 0;
        var failed = 0;

        foreach (var path in pathsList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await SyncAssetAsync(path, cancellationToken);
            processed++;

            if (result != null && !string.IsNullOrEmpty(result.Message))
            {
                successful++;
                yield return new SyncProgressUpdate
                {
                    Current = processed,
                    Total = total,
                    Successful = successful,
                    Failed = failed,
                    CurrentPath = path,
                    Message = $"Sincronizado: {Path.GetFileName(path)}",
                    IsCompleted = processed == total
                };
            }
            else
            {
                failed++;
                yield return new SyncProgressUpdate
                {
                    Current = processed,
                    Total = total,
                    Successful = successful,
                    Failed = failed,
                    CurrentPath = path,
                    Message = $"Error al sincronizar: {Path.GetFileName(path)}",
                    IsCompleted = processed == total
                };
            }
        }
    }
}
