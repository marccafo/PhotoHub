using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.Api;

public static class DependencyInjection
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DirectoryScanner>();
    }

    public static void AddPostgres(this WebApplicationBuilder builder)
    {
        // Configurar Entity Framework Core con PostgreSQL
        var connectionString = builder.Configuration.GetConnectionString("Postgres") 
            ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

        builder.Services.AddDbContext<PhotoDbContext>(options =>
            options.UseNpgsql(connectionString));
    }

    public static void RegisterEndpoints(this IEndpointRouteBuilder app)
    {
        var endpointTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false });

        foreach (var type in endpointTypes)
        {
            // Los endpoints ya no tienen dependencias scoped en el constructor,
            // así que podemos crearlos directamente sin scope
            var endpoint = (IEndpoint)Activator.CreateInstance(type)!;
            endpoint.MapEndpoint(app);
        }
    }

    public static void ExecuteMigrations(this WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PhotoDbContext>();
            try
            {
                dbContext.Database.Migrate();
                Console.WriteLine("Database migrations applied successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying migrations: {ex.Message}");
                // No lanzar excepción para permitir que la app continúe
            }
        }
    }
}