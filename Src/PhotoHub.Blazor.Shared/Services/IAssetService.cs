using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IAssetService
{
    Task<List<TimelineItem>> GetTimelineAsync();
    Task<TimelineItem?> GetAssetByIdAsync(int id);
    Task<AssetDetail?> GetAssetDetailAsync(int id);
    Task<List<TimelineItem>> GetAssetsByFolderAsync(int? folderId);
    Task<UploadResponse?> UploadAssetAsync(string fileName, Stream content, CancellationToken cancellationToken = default);
}

public class UploadResponse
{
    public string Message { get; set; } = string.Empty;
    public int? AssetId { get; set; }
}
