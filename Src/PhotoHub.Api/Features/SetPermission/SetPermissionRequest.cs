namespace PhotoHub.API.Features.SetPermission;

public class SetPermissionRequest
{
    public int UserId { get; set; }
    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
    public bool CanDelete { get; set; }
    public bool CanManagePermissions { get; set; }
    public int? GrantedByUserId { get; set; }
}

