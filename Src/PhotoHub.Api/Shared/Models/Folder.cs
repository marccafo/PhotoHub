using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class Folder
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(1000)]
    public string Path { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;
    
    public int? ParentFolderId { get; set; }
    public Folder? ParentFolder { get; set; }
    public ICollection<Folder> SubFolders { get; set; } = new List<Folder>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public ICollection<FolderPermission> Permissions { get; set; } = new List<FolderPermission>();
}

