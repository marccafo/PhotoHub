using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;
using Xabe.FFmpeg;

namespace PhotoHub.Api;

public static class DependencyInjection
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DirectoryScanner>();
        builder.Services.AddScoped<FileHashService>();
        builder.Services.AddScoped<ExifExtractorService>();
        builder.Services.AddScoped<ThumbnailGeneratorService>();
        builder.Services.AddScoped<MediaRecognitionService>();
        builder.Services.AddScoped<IMlJobService, MlJobService>();
        builder.Services.AddHostedService<MlJobProcessorService>();

        // Configure FFmpeg path if provided in configuration
        var ffmpegPath = builder.Configuration["FFMPEG_PATH"];
        if (!string.IsNullOrEmpty(ffmpegPath))
        {
            FFmpeg.SetExecutablesPath(ffmpegPath);
        }
        else
        {
            // Try common locations if not set
            var commonPaths = new[] { 
                @"C:\ffmpeg\bin", 
                @"C:\Program Files\ffmpeg\bin",
                Path.Combine(Directory.GetCurrentDirectory(), "ffmpeg", "bin")
            };
            
            foreach (var path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    FFmpeg.SetExecutablesPath(path);
                    Console.WriteLine($"[INFO] FFmpeg path set to: {path}");
                    
                    // Check if both ffmpeg and ffprobe exist
                    var ffmpegExe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                    var ffprobeExe = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";
                    
                    if (!File.Exists(Path.Combine(path, ffmpegExe)))
                        Console.WriteLine($"[WARNING] {ffmpegExe} not found in {path}");
                    if (!File.Exists(Path.Combine(path, ffprobeExe)))
                        Console.WriteLine($"[WARNING] {ffprobeExe} not found in {path}");
                        
                    break;
                }
            }
        }
    }

    public static void AddPostgres(this WebApplicationBuilder builder)
    {
        // Configurar Entity Framework Core con PostgreSQL
        var connectionString = builder.Configuration.GetConnectionString("Postgres") 
            ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
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
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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