using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.API.Features.Timeline;

public class TimelineResponse
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Extension { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string Type { get; set; } = string.Empty; // IMAGE or VIDEO
    public string Checksum { get; set; } = string.Empty;
    public bool HasExif { get; set; }
    public bool HasThumbnails { get; set; }
    public AssetSyncStatus SyncStatus { get; set; }
}

