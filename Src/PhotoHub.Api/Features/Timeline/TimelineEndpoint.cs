using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;
using PhotoHub.Blazor.Shared.Models;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Timeline;

public class TimelineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/timeline", Handle)
        .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/assets/timeline\" -H \"Accept: application/json\"",
                label: "cURL Example")
        .WithName("GetTimeline")
        .WithTags("Assets")
        .WithDescription("Gets the timeline of all scanned media files (images and videos)")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Gets the timeline";
            operation.Description = "Returns all media assets stored in the database, ordered by the most recently scanned first, then by modification date";
            return Task.CompletedTask;
        });
    }

    private async Task<IResult> Handle(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] DirectoryScanner directoryScanner,
        [FromServices] SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var assets = await dbContext.Assets
                .Include(a => a.Exif)
                .Include(a => a.Thumbnails)
                .OrderByDescending(a => a.ScannedAt)
                .ThenByDescending(a => a.ModifiedDate)
                .ToListAsync(cancellationToken);

            var timelineItems = assets.Select(asset => new TimelineResponse
            {
                Id = asset.Id,
                FileName = asset.FileName,
                FullPath = asset.FullPath,
                FileSize = asset.FileSize,
                CreatedDate = asset.CreatedDate,
                ModifiedDate = asset.ModifiedDate,
                Extension = asset.Extension,
                ScannedAt = asset.ScannedAt,
                Type = asset.Type.ToString(),
                Checksum = asset.Checksum,
                HasExif = asset.Exif != null,
                HasThumbnails = asset.Thumbnails.Any(),
                SyncStatus = AssetSyncStatus.Synced,
                Width = asset.Exif?.Width,
                Height = asset.Exif?.Height
            }).ToList();

            // Normalizar rutas existentes en BD para comparación
            var existingPaths = assets
                .Select(a => a.FullPath.Replace('\\', '/').TrimEnd('/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Crear un set de nombres de archivos indexados para detectar duplicados
            var indexedFileNames = assets
                .Select(a => a.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // 1. Primero detectar archivos copiados pero no indexados en el directorio interno
            var internalAssetsPath = settingsService.GetInternalAssetsPath();
            int copiedCount = 0;
            var copiedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var internalScannedFiles = new List<ScannedFile>();
            
            if (Directory.Exists(internalAssetsPath))
            {
                Console.WriteLine($"[DEBUG] Scanning internal directory for copied but not indexed assets: {internalAssetsPath}");
                internalScannedFiles = (await directoryScanner.ScanDirectoryAsync(internalAssetsPath, cancellationToken)).ToList();
                Console.WriteLine($"[DEBUG] Found {internalScannedFiles.Count} files in internal directory");
                
                // Resolver rutas físicas de assets en BD para comparar
                var existingPhysicalPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var asset in assets)
                {
                    var physicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
                    if (!string.IsNullOrEmpty(physicalPath))
                    {
                        existingPhysicalPaths.Add(Path.GetFullPath(physicalPath).Replace('\\', '/'));
                    }
                }
                
                // Crear un set de rutas físicas normalizadas para comparación rápida
                var existingPhysicalPathsNormalized = existingPhysicalPaths
                    .Select(p => Path.GetFileName(p))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                foreach (var file in internalScannedFiles)
                {
                    var normalizedFilePath = Path.GetFullPath(file.FullPath).Replace('\\', '/');
                    
                    // Si el archivo está en el directorio interno pero no está en BD, está copiado pero no indexado
                    if (!existingPhysicalPaths.Contains(normalizedFilePath))
                    {
                        // Verificar si hay un archivo con el mismo nombre ya indexado (puede tener ruta diferente)
                        var fileName = file.FileName;
                        if (!existingPhysicalPathsNormalized.Contains(fileName))
                        {
                            // Virtualizar la ruta para mostrarla en el timeline
                            var virtualizedPath = await settingsService.VirtualizePathAsync(file.FullPath);
                            
                            copiedFileNames.Add(fileName);
                            copiedCount++;
                            timelineItems.Add(new TimelineResponse
                            {
                                Id = 0,
                                FileName = fileName,
                                FullPath = virtualizedPath,
                                FileSize = file.FileSize,
                                CreatedDate = file.CreatedDate,
                                ModifiedDate = file.ModifiedDate,
                                Extension = file.Extension,
                                ScannedAt = DateTime.MinValue,
                                Type = file.AssetType.ToString(),
                                SyncStatus = AssetSyncStatus.Copied,
                                Width = null, // Se puede obtener más tarde si es necesario
                                Height = null
                            });
                        }
                    }
                }
                Console.WriteLine($"[DEBUG] Identified {copiedCount} copied but not indexed assets to show in timeline");
            }

            // Los assets pendientes del dispositivo se gestionan en la página "Mi Dispositivo" (/device)
            // No se incluyen en el timeline principal para mantenerlo más ligero

            // Re-order by most recent date (preferring CreatedDate but handles cases where only ModifiedDate is available)
            var orderedTimeline = timelineItems
                .OrderByDescending(a => a.SyncStatus == AssetSyncStatus.Pending || a.SyncStatus == AssetSyncStatus.Copied ? a.ModifiedDate : a.CreatedDate)
                .ThenByDescending(a => a.FileName)
                .ToList();

            return Results.Ok(orderedTimeline);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}

