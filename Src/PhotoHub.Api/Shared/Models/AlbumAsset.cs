using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class AlbumAsset
{
    public int Id { get; set; }
    
    public int AlbumId { get; set; }
    public Album Album { get; set; } = null!;
    
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    
    // Orden personalizado dentro del álbum
    public int Order { get; set; }
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
