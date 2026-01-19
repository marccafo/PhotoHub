namespace PhotoHub.API.Shared.Models;

public class AlbumAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid AlbumId { get; set; }
    public Album Album { get; set; } = null!;
    
    public Guid AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    
    // Orden personalizado dentro del álbum
    public int Order { get; set; }
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
