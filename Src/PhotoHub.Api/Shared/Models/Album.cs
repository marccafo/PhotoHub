using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class Album
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Cover image (thumbnail del primer asset o uno seleccionado)
    public int? CoverAssetId { get; set; }
    public Asset? CoverAsset { get; set; }
    
    // Navigation properties
    public ICollection<AlbumAsset> AlbumAssets { get; set; } = new List<AlbumAsset>();
}
