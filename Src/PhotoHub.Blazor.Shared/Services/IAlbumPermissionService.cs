namespace PhotoHub.Blazor.Shared.Services;

public interface IAlbumPermissionService
{
    Task<List<AlbumPermissionDto>> GetAlbumPermissionsAsync(int albumId);
    Task<AlbumPermissionDto> SetAlbumPermissionAsync(int albumId, SetAlbumPermissionRequest request);
    Task DeleteAlbumPermissionAsync(int albumId, int userId);
}

public class AlbumPermissionDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    public DateTime GrantedAt { get; set; }
    public int? GrantedByUserId { get; set; }
}

public class SetAlbumPermissionRequest
{
    public int UserId { get; set; }
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
}
