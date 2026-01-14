namespace PhotoHub.Blazor.Shared.Models;

public class AlbumItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AssetCount { get; set; }
    public string? CoverThumbnailUrl { get; set; }
    public bool IsOwner { get; set; }
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
}
