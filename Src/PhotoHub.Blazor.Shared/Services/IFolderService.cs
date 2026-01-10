using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IFolderService
{
    Task<List<FolderItem>> GetFoldersAsync();
    Task<FolderItem?> GetFolderByIdAsync(int id);
    Task<List<FolderItem>> GetFolderTreeAsync();
    Task<List<TimelineItem>> GetFolderAssetsAsync(int folderId);
}
