using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IFolderService
{
    Task<List<FolderItem>> GetFoldersAsync();
    Task<FolderItem?> GetFolderByIdAsync(Guid id);
    Task<List<FolderItem>> GetFolderTreeAsync();
    Task<List<TimelineItem>> GetFolderAssetsAsync(Guid folderId);
    Task<FolderItem> CreateFolderAsync(CreateFolderRequest request);
    Task<FolderItem> UpdateFolderAsync(Guid folderId, UpdateFolderRequest request);
    Task DeleteFolderAsync(Guid folderId);
    Task MoveFolderAssetsAsync(MoveFolderAssetsRequest request);
    Task RemoveFolderAssetsAsync(RemoveFolderAssetsRequest request);
}
