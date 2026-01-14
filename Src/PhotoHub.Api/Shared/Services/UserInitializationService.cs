using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Shared.Services;

public class UserInitializationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;

    public UserInitializationService(
        ApplicationDbContext dbContext,
        IAuthService authService,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _authService = authService;
        _configuration = configuration;
    }

    public async Task InitializeAdminUserAsync(CancellationToken cancellationToken = default)
    {
        var adminUsername = _configuration["AdminUser:Username"];
        var adminEmail = _configuration["AdminUser:Email"];
        var adminPassword = _configuration["AdminUser:Password"];

        // Si no hay configuración, no crear usuario admin
        if (string.IsNullOrWhiteSpace(adminUsername) || 
            string.IsNullOrWhiteSpace(adminEmail) || 
            string.IsNullOrWhiteSpace(adminPassword))
        {
            Console.WriteLine("[INFO] No se configuró usuario administrador inicial. Saltando inicialización.");
            return;
        }

        // Verificar si ya existe un usuario con ese username o email
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == adminUsername || u.Email == adminEmail, cancellationToken);

        if (existingUser != null)
        {
            Console.WriteLine($"[INFO] Usuario administrador '{adminUsername}' ya existe. Saltando inicialización.");
            return;
        }

        // Crear usuario administrador
        var adminUser = new User
        {
            Username = adminUsername,
            Email = adminEmail,
            PasswordHash = _authService.HashPassword(adminPassword),
            FirstName = _configuration["AdminUser:FirstName"] ?? "Administrador",
            LastName = _configuration["AdminUser:LastName"] ?? "Sistema",
            Role = "Admin",
            IsActive = true,
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(adminUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"[INFO] Usuario administrador '{adminUsername}' creado exitosamente.");
    }
}
