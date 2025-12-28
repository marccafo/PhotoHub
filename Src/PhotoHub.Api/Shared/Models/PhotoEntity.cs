using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class PhotoEntity
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(1000)]
    public string FullPath { get; set; } = string.Empty;
    
    public long FileSize { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    public DateTime ModifiedDate { get; set; }
    
    [Required]
    [MaxLength(10)]
    public string Extension { get; set; } = string.Empty;
    
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    
    // Offset del timezone en minutos desde UTC (ej: -300 para UTC-5, 120 para UTC+2)
    public int TimeZoneOffsetMinutes { get; set; }
}

