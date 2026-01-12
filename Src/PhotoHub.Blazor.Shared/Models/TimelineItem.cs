namespace PhotoHub.Blazor.Shared.Models;

public class TimelineItem
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
    public AssetSyncStatus SyncStatus { get; set; } = AssetSyncStatus.Synced;
    
    public string ThumbnailUrl => SyncStatus == AssetSyncStatus.Synced 
        ? $"/api/assets/{Id}/thumbnail?size=Medium" 
        : $"/api/assets/pending/content?path={System.Net.WebUtility.UrlEncode(FullPath)}";
    public string ContentUrl => SyncStatus == AssetSyncStatus.Synced 
        ? $"/api/assets/{Id}/content" 
        : $"/api/assets/pending/content?path={System.Net.WebUtility.UrlEncode(FullPath)}";
    public string DisplayDate => CreatedDate.ToString("dd MMM yyyy");
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
