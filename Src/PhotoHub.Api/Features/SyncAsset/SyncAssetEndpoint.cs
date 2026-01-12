using Microsoft.AspNetCore.Mvc;
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

            // Manejar colisiones de nombres
            if (File.Exists(targetPath))
            {
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
