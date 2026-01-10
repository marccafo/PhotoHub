namespace PhotoHub.Blazor.Shared.Models;

public class AssetDetail
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public bool HasExif { get; set; }
    public bool HasThumbnails { get; set; }
    public int? FolderId { get; set; }
    public string? FolderPath { get; set; }
    public ExifData? Exif { get; set; }
    public List<ThumbnailInfo> Thumbnails { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    
    public string ThumbnailUrl => $"/api/assets/{Id}/thumbnail?size=Large";
    public string ContentUrl => $"/api/assets/{Id}/content";
    public string DisplayDate => CreatedDate.ToString("dd MMM yyyy HH:mm");
    public string FileSizeFormatted => FormatFileSize(FileSize);
    
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

public class ExifData
{
    public DateTime? DateTaken { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Orientation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Software { get; set; }
}

public class ThumbnailInfo
{
    public int Id { get; set; }
    public string Size { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Url => $"/api/assets/{AssetId}/thumbnail?size={Size}";
    public int AssetId { get; set; }
}
