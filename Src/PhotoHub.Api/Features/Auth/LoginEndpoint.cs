using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Auth;

public class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Handle)
            .WithName("Login")
            .WithTags("Authentication")
            .AllowAnonymous()
            .WithDescription("Authenticates a user and returns a JWT token");
    }

    private async Task<IResult> Handle(
        [FromBody] LoginRequest request,
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Username and password are required" });
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Username, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        if (!authService.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        // Actualizar último login
        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = await authService.GenerateTokenAsync(user);

        return Results.Ok(new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                FirstName = user.FirstName,
                LastName = user.LastName
            }
        });
    }
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
