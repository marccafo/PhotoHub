namespace PhotoHub.Blazor.Shared.Models;

public class FolderItem
{
    public Guid Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AssetCount { get; set; }
    public bool IsShared { get; set; }
    public bool IsOwner { get; set; }
    public int SharedWithCount { get; set; }
    public List<FolderItem> SubFolders { get; set; } = new();
}
