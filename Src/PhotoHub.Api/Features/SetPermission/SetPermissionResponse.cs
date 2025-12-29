namespace PhotoHub.API.Features.SetPermission;

public class SetPermissionResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int FolderId { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    public DateTime GrantedAt { get; set; }
    public int? GrantedByUserId { get; set; }
    public string? GrantedByUsername { get; set; }
}

