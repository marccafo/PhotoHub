using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Auth;

public class RegisterEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/register", Handle)
            .WithName("Register")
            .WithTags("Authentication")
            .AllowAnonymous()
            .WithDescription("Registers a new user account");
    }

    private async Task<IResult> Handle(
        [FromBody] RegisterRequest request,
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

        // Validar que el usuario no exista
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
            Role = "User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = await authService.GenerateTokenAsync(user);

        return Results.Created($"/api/users/{user.Id}", new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            }
        });
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
