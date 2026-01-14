namespace PhotoHub.Blazor.Shared.Services;

public interface IUserService
{
    Task<List<UserDto>> GetUsersAsync();
    Task<UserDto> GetUserAsync(int id);
    Task<UserDto> GetCurrentUserAsync();
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request);
    Task DeleteUserAsync(int id);
    Task ResetPasswordAsync(int id, string newPassword);
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateUserRequest
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}
