using PhotoHub.API.Features.Timeline;

namespace PhotoHub.API.Features.Folders;

public class FolderResponse
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AssetCount { get; set; }
    public List<FolderResponse> SubFolders { get; set; } = new();
}
