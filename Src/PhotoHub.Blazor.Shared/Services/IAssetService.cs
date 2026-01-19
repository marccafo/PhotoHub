using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IAssetService
{
    Task<List<TimelineItem>> GetTimelineAsync();
    Task<List<TimelineItem>> GetDeviceAssetsAsync();
    Task<TimelineItem?> GetAssetByIdAsync(Guid id);
    Task<AssetDetail?> GetAssetDetailAsync(Guid id);
    Task<AssetDetail?> GetPendingAssetDetailAsync(string path);
    Task<List<TimelineItem>> GetAssetsByFolderAsync(Guid? folderId);
    Task<UploadResponse?> UploadAssetAsync(string fileName, Stream content, CancellationToken cancellationToken = default);
    Task<SyncAssetResponse?> SyncAssetAsync(string path, CancellationToken cancellationToken = default);
    IAsyncEnumerable<SyncProgressUpdate> SyncMultipleAssetsAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default);
    Task DeleteAssetsAsync(DeleteAssetsRequest request);
    Task RestoreAssetsAsync(RestoreAssetsRequest request);
    Task PurgeAssetsAsync(PurgeAssetsRequest request);
    Task RestoreTrashAsync();
    Task EmptyTrashAsync();
}

public class UploadResponse
{
    public string Message { get; set; } = string.Empty;
    public Guid? AssetId { get; set; }
}

public class SyncAssetResponse
{
    public string Message { get; set; } = string.Empty;
    public Guid? AssetId { get; set; } // Opcional: solo se devuelve si el asset ya estaba indexado
    public string? TargetPath { get; set; } // Ruta donde se copió el archivo
}
