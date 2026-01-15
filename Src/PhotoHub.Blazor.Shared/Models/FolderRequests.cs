namespace PhotoHub.Blazor.Shared.Models;

public class CreateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
}

public class UpdateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
}

public class MoveFolderAssetsRequest
{
    public int SourceFolderId { get; set; }
    public int TargetFolderId { get; set; }
    public List<int> AssetIds { get; set; } = new();
}
