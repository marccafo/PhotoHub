using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IFolderService
{
    Task<List<FolderItem>> GetFoldersAsync();
    Task<FolderItem?> GetFolderByIdAsync(int id);
    Task<List<FolderItem>> GetFolderTreeAsync();
    Task<List<TimelineItem>> GetFolderAssetsAsync(int folderId);
    Task<FolderItem> CreateFolderAsync(CreateFolderRequest request);
    Task<FolderItem> UpdateFolderAsync(int folderId, UpdateFolderRequest request);
    Task DeleteFolderAsync(int folderId);
    Task MoveFolderAssetsAsync(MoveFolderAssetsRequest request);
}
