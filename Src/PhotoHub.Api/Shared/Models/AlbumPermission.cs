using System.ComponentModel.DataAnnotations;

namespace PhotoHub.API.Shared.Models;

public class AlbumPermission
{
    public int Id { get; set; }
    
    public int AlbumId { get; set; }
    public Album Album { get; set; } = null!;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    
    public int? GrantedByUserId { get; set; }
    public User? GrantedByUser { get; set; }
}
