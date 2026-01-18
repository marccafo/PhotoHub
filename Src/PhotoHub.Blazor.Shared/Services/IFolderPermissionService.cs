namespace PhotoHub.Blazor.Shared.Services;

public interface IFolderPermissionService
{
    Task<List<FolderPermissionDto>> GetFolderPermissionsAsync(int folderId);
    Task<FolderPermissionDto> SetFolderPermissionAsync(int folderId, SetFolderPermissionRequest request);
    Task DeleteFolderPermissionAsync(int folderId, int userId);
}

public class FolderPermissionDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    public DateTime GrantedAt { get; set; }
    public int? GrantedByUserId { get; set; }
}

public class SetFolderPermissionRequest
{
    public int UserId { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
}
