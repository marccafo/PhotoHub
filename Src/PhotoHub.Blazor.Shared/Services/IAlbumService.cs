using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IAlbumService
{
    Task<List<AlbumItem>> GetAlbumsAsync();
    Task<AlbumItem?> GetAlbumByIdAsync(int id);
    Task<List<TimelineItem>> GetAlbumAssetsAsync(int albumId);
    Task<AlbumItem?> CreateAlbumAsync(string name, string? description);
    Task<bool> UpdateAlbumAsync(int id, string name, string? description);
    Task<bool> DeleteAlbumAsync(int id);
    Task<bool> LeaveAlbumAsync(int albumId);
    Task<bool> AddAssetToAlbumAsync(int albumId, int assetId);
    Task<bool> RemoveAssetFromAlbumAsync(int albumId, int assetId);
    Task<bool> SetAlbumCoverAsync(int albumId, int assetId);
}
