using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class FolderPermission
{
    public int Id { get; set; }
    
    [Required]
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    [Required]
    public int FolderId { get; set; }
    public Folder Folder { get; set; } = null!;
    
    // Permission flags
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    
    public int? GrantedByUserId { get; set; }
    public User? GrantedByUser { get; set; }
}

