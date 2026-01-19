using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Features.Auth;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Users;

public class UsersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("", GetAllUsers)
            .WithName("GetAllUsers")
            .WithDescription("Gets all users (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapGet("{id:guid}", GetUser)
            .WithName("GetUser")
            .WithDescription("Gets a user by ID (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapGet("me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithDescription("Gets the current authenticated user");

        group.MapPost("", CreateUser)
            .WithName("CreateUser")
            .WithDescription("Creates a new user (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPut("{id:guid}", UpdateUser)
            .WithName("UpdateUser")
            .WithDescription("Updates a user (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapDelete("{id:guid}", DeleteUser)
            .WithName("DeleteUser")
            .WithDescription("Deletes a user (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapPost("{id:guid}/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .WithDescription("Resets a user's password (Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));
    }

    private async Task<IResult> GetAllUsers(
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(users);
    }

    private async Task<IResult> GetUser(
        Guid id,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user == null)
            return Results.NotFound();

        return Results.Ok(user);
    }

    private async Task<IResult> GetCurrentUser(
        ClaimsPrincipal user,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Results.Unauthorized();
        }

        var currentUser = await dbContext.Users
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role,
                FirstName = u.FirstName,
                LastName = u.LastName,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (currentUser == null)
            return Results.NotFound();

        return Results.Ok(currentUser);
    }

    private async Task<IResult> CreateUser(
        [FromBody] CreateUserRequest request,
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { error = "Username, email and password are required" });
        }

        // Validar contraseña
        var passwordValidation = authService.ValidatePassword(request.Password);
        if (!passwordValidation.IsValid)
        {
            return Results.BadRequest(new { error = passwordValidation.ErrorMessage });
        }

        if (await dbContext.Users.AnyAsync(u => u.Username == request.Username || u.Email == request.Email, cancellationToken))
        {
            return Results.BadRequest(new { error = "Username or email already exists" });
        }

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = authService.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = request.Role ?? "User",
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/users/{user.Id}", new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }

    private async Task<IResult> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequest request,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
            return Results.NotFound();

        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            if (await dbContext.Users.AnyAsync(u => u.Username == request.Username && u.Id != id, cancellationToken))
                return Results.BadRequest(new { error = "Username already exists" });
            user.Username = request.Username;
        }

        if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
        {
            if (await dbContext.Users.AnyAsync(u => u.Email == request.Email && u.Id != id, cancellationToken))
                return Results.BadRequest(new { error = "Email already exists" });
            user.Email = request.Email;
        }

        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.Role != null) user.Role = request.Role;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        });
    }

    private async Task<IResult> DeleteUser(
        Guid id,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
            return Results.NotFound();

        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private async Task<IResult> ResetPassword(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new { error = "New password is required" });
        }

        // Validar contraseña
        var passwordValidation = authService.ValidatePassword(request.NewPassword);
        if (!passwordValidation.IsValid)
        {
            return Results.BadRequest(new { error = passwordValidation.ErrorMessage });
        }

        var user = await dbContext.Users.FindAsync(new object[] { id }, cancellationToken);
        if (user == null)
            return Results.NotFound();

        user.PasswordHash = authService.HashPassword(request.NewPassword);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { message = "Password reset successfully" });
    }
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
