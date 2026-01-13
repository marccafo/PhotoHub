using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.SyncAsset;

public class SyncAssetEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/assets/sync", Handle)
            .WithName("SyncAsset")
            .WithTags("Assets")
            .WithDescription("Copies a pending asset from the user's device to the internal assets directory. The file will be indexed when the scan process runs.");
    }

    private async Task<IResult> Handle(
        [FromQuery] string path,
        [FromServices] SettingsService settingsService,
        [FromServices] FileHashService hashService,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
            return Results.BadRequest("La ruta es obligatoria");

        try
        {
            // Validar que el archivo proviene de la ruta configurada por el usuario
            var userConfiguredPath = await settingsService.GetAssetsPathAsync();
            if (!IsPathSafe(path, userConfiguredPath))
                return Results.Forbid();

            if (!File.Exists(path))
                return Results.NotFound("El archivo no existe en el disco");

            // Obtener la ruta interna del NAS (ASSETS_PATH)
            var managedLibraryPath = settingsService.GetInternalAssetsPath();

            if (!Directory.Exists(managedLibraryPath))
            {
                Directory.CreateDirectory(managedLibraryPath);
                Console.WriteLine($"[SYNC] Created internal assets directory: {managedLibraryPath}");
            }
            
            Console.WriteLine($"[SYNC] Source path: {path}");
            Console.WriteLine($"[SYNC] Target internal path: {managedLibraryPath}");

            var currentFileInfo = new FileInfo(path);
            var fileName = currentFileInfo.Name;
            var targetPath = Path.Combine(managedLibraryPath, fileName);

            // Normalizar rutas para comparación
            var normalizedPath = Path.GetFullPath(path);
            var normalizedLibraryPath = Path.GetFullPath(managedLibraryPath);

            // Si el archivo ya está en el directorio interno, no hacer nada
            if (normalizedPath.StartsWith(normalizedLibraryPath, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[SYNC] File is already in internal directory: {path}");
                return Results.Ok(new { 
                    message = "El archivo ya está en el directorio interno", 
                    targetPath = path 
                });
            }

            // Calcular checksum del archivo fuente para verificar duplicados
            Console.WriteLine($"[SYNC] Calculating checksum for source file: {path}");
            var sourceChecksum = await hashService.CalculateFileHashAsync(path, cancellationToken);
            Console.WriteLine($"[SYNC] Source checksum: {sourceChecksum}");

            // Verificar si ya existe un archivo con el mismo checksum en el directorio interno
            var existingAsset = await dbContext.Assets
                .FirstOrDefaultAsync(a => a.Checksum == sourceChecksum, cancellationToken);
            
            if (existingAsset != null)
            {
                // Resolver la ruta física del asset existente
                var existingPhysicalPath = await settingsService.ResolvePhysicalPathAsync(existingAsset.FullPath);
                
                if (!string.IsNullOrEmpty(existingPhysicalPath) && File.Exists(existingPhysicalPath))
                {
                    Console.WriteLine($"[SYNC] File with same checksum already exists: {existingPhysicalPath}");
                    return Results.Ok(new { 
                        message = "El archivo ya existe en el directorio interno (mismo contenido)", 
                        targetPath = existingPhysicalPath 
                    });
                }
            }

            // Si no está en BD, verificar si el archivo con el nombre ya existe y tiene el mismo checksum
            // (para evitar copiar el mismo archivo múltiples veces)
            if (File.Exists(targetPath))
            {
                try
                {
                    var existingChecksum = await hashService.CalculateFileHashAsync(targetPath, cancellationToken);
                    if (existingChecksum == sourceChecksum)
                    {
                        Console.WriteLine($"[SYNC] File with same name and checksum already exists: {targetPath}");
                        return Results.Ok(new { 
                            message = "El archivo ya existe en el directorio interno", 
                            targetPath = targetPath 
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SYNC] Warning: Could not calculate checksum for existing file {targetPath}: {ex.Message}");
                }
            }

            // Manejar colisiones de nombres (solo si el archivo existe pero tiene diferente checksum)
            if (File.Exists(targetPath))
            {
                // Verificar si el archivo existente tiene el mismo checksum
                var existingChecksum = await hashService.CalculateFileHashAsync(targetPath, cancellationToken);
                if (existingChecksum == sourceChecksum)
                {
                    Console.WriteLine($"[SYNC] File with same name and checksum already exists: {targetPath}");
                    return Results.Ok(new { 
                        message = "El archivo ya existe en el directorio interno", 
                        targetPath = targetPath 
                    });
                }
                // Si tiene diferente checksum, crear con nombre único
                targetPath = Path.Combine(managedLibraryPath, $"{Guid.NewGuid()}_{fileName}");
            }

            // Preservar fechas originales
            var originalCreation = currentFileInfo.CreationTimeUtc;
            var originalLastWrite = currentFileInfo.LastWriteTimeUtc;

            // Copiar el archivo
            Console.WriteLine($"[SYNC] Copying file from {path} to {targetPath}");
            File.Copy(path, targetPath, overwrite: false);
            Console.WriteLine($"[SYNC] File copied successfully");

            // Aplicar metadatos de tiempo
            File.SetCreationTimeUtc(targetPath, originalCreation);
            File.SetLastWriteTimeUtc(targetPath, originalLastWrite);
            
            // Verificar que el archivo se copió correctamente
            if (!File.Exists(targetPath))
            {
                throw new Exception($"Error: El archivo no se copió correctamente a {targetPath}");
            }
            
            Console.WriteLine($"[SYNC] File verified at target path: {targetPath}");

            return Results.Ok(new { 
                message = "Archivo sincronizado correctamente. Ejecuta la indexación para indexarlo.", 
                targetPath = targetPath 
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SYNC ERROR] Error al sincronizar asset: {ex.Message}");
            Console.WriteLine($"[SYNC ERROR] Stack trace: {ex.StackTrace}");
            return Results.Problem($"Error al sincronizar el asset: {ex.Message}");
        }
    }

    private bool IsPathSafe(string path, string assetsPath)
    {
        var fullPath = Path.GetFullPath(path);
        var fullAssetsPath = Path.GetFullPath(assetsPath);
        return fullPath.StartsWith(fullAssetsPath, StringComparison.OrdinalIgnoreCase);
    }
}
