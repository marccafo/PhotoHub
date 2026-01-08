using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public enum AssetType
{
    IMAGE,
    VIDEO
}

public class Asset
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string FullPath { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    [Required]
    [MaxLength(64)]
    public string Checksum { get; set; } = string.Empty; // SHA256 hash
    
    public AssetType Type { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    public DateTime ModifiedDate { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Extension { get; set; } = string.Empty;
    
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }
    
    public int? FolderId { get; set; }
    public Folder? Folder { get; set; }
    
    // For videos
    public TimeSpan? Duration { get; set; }
    
    // Navigation properties
    public AssetExif? Exif { get; set; }
    public ICollection<AssetThumbnail> Thumbnails { get; set; } = new List<AssetThumbnail>();
    public ICollection<AssetTag> Tags { get; set; } = new List<AssetTag>();
    public ICollection<AssetMlJob> MlJobs { get; set; } = new List<AssetMlJob>();
}

