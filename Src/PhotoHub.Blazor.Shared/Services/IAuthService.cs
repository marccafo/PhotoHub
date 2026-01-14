namespace PhotoHub.Blazor.Shared.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<string?> GetTokenAsync();
    Task<UserDto?> GetCurrentUserAsync();
    event Action? OnAuthStateChanged;
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
