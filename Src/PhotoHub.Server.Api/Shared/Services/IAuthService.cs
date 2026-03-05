using PhotoHub.Server.Api.Shared.Models;

namespace PhotoHub.Server.Api.Shared.Services;

public interface IAuthService
{
    Task<string> GenerateTokenAsync(User user);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    (bool IsValid, string? ErrorMessage) ValidatePassword(string password);
}
