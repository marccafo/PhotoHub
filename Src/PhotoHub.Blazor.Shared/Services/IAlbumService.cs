using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IAlbumService
{
    Task<List<AlbumItem>> GetAlbumsAsync();
    Task<AlbumItem?> GetAlbumByIdAsync(Guid id);
    Task<List<TimelineItem>> GetAlbumAssetsAsync(Guid albumId);
    Task<AlbumItem?> CreateAlbumAsync(string name, string? description);
    Task<bool> UpdateAlbumAsync(Guid id, string name, string? description);
    Task<bool> DeleteAlbumAsync(Guid id);
    Task<bool> LeaveAlbumAsync(Guid albumId);
    Task<bool> AddAssetToAlbumAsync(Guid albumId, Guid assetId);
    Task<bool> RemoveAssetFromAlbumAsync(Guid albumId, Guid assetId);
    Task<bool> SetAlbumCoverAsync(Guid albumId, Guid assetId);
}
