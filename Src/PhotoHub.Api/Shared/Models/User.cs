using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public ICollection<FolderPermission> FolderPermissions { get; set; } = new List<FolderPermission>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}

