using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
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
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var isAdmin = user.IsInRole("Admin");
            var userRootPath = GetUserRootPath(userId);

            var query = dbContext.Assets
                .Include(a => a.Exif)
                .Include(a => a.Thumbnails)
                .Where(a => a.DeletedAt == null);

            if (!isAdmin)
            {
                // Filtrar por permisos de carpeta
                var allowedFolderIds = await GetAllowedFolderIdsForUserAsync(dbContext, userId, userRootPath, cancellationToken);
                
                query = query.Where(a => a.FolderId.HasValue && allowedFolderIds.Contains(a.FolderId.Value));
            }

            var assets = await query
                .OrderByDescending(a => a.ScannedAt)
                .ThenByDescending(a => a.ModifiedDate)
                .ToListAsync(cancellationToken);

            // Obtener rutas de carpetas permitidas para filtrar assets no indexados
            var allowedFolderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!isAdmin)
            {
                allowedFolderPaths = await GetAllowedFolderPathsForUserAsync(dbContext, userId, userRootPath, cancellationToken);
            }

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
                Height = asset.Exif?.Height,
                DeletedAt = asset.DeletedAt
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
                            
                            // Si no es admin, filtrar por rutas de carpetas permitidas
                            if (!isAdmin)
                            {
                                var isAllowed = false;
                                foreach (var allowedPath in allowedFolderPaths)
                                {
                                    if (virtualizedPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isAllowed = true;
                                        break;
                                    }
                                }
                                
                                if (!isAllowed) continue;
                            }

                            copiedFileNames.Add(fileName);
                            copiedCount++;
                            timelineItems.Add(new TimelineResponse
                            {
                                Id = Guid.Empty,
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

    private bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim?.Value, out userId);
    }

    private string GetUserRootPath(Guid userId)
    {
        return $"/assets/users/{userId}";
    }

    private async Task<HashSet<Guid>> GetAllowedFolderIdsForUserAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        string userRootPath,
        CancellationToken ct)
    {
        var allFolders = await dbContext.Folders.ToListAsync(ct);
        var permissions = await dbContext.FolderPermissions
            .Where(p => p.UserId == userId && p.CanRead)
            .ToListAsync(ct);

        var foldersWithPermissions = await dbContext.FolderPermissions
            .Select(p => p.FolderId)
            .Distinct()
            .ToListAsync(ct);
        
        var foldersWithPermissionsSet = foldersWithPermissions.ToHashSet();

        var allowedIds = permissions.Select(p => p.FolderId).ToHashSet();

        foreach (var folder in allFolders)
        {
            if (!foldersWithPermissionsSet.Contains(folder.Id))
            {
                if (folder.Path.Replace('\\', '/').StartsWith(userRootPath, StringComparison.OrdinalIgnoreCase))
                {
                    allowedIds.Add(folder.Id);
                }
            }
        }

        // Añadir ancestros para consistencia, aunque para assets quizás no es estrictamente necesario 
        // si solo queremos assets de carpetas finales permitidas.
        return allowedIds;
    }

    private async Task<HashSet<string>> GetAllowedFolderPathsForUserAsync(
        ApplicationDbContext dbContext,
        Guid userId,
        string userRootPath,
        CancellationToken ct)
    {
        var allFolders = await dbContext.Folders.ToListAsync(ct);
        var permissions = await dbContext.FolderPermissions
            .Where(p => p.UserId == userId && p.CanRead)
            .ToListAsync(ct);

        var foldersWithPermissionsSet = await dbContext.FolderPermissions
            .Select(p => p.FolderId)
            .Distinct()
            .ToHashSetAsync(ct);

        var allowedPaths = permissions
            .Select(p => allFolders.FirstOrDefault(f => f.Id == p.FolderId)?.Path)
            .Where(p => p != null)
            .Select(p => p!.Replace('\\', '/').TrimEnd('/') + "/")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Añadir espacio personal
        allowedPaths.Add(userRootPath.TrimEnd('/') + "/");

        // Añadir carpetas que no tienen permisos explícitos pero están en espacio personal (ya cubierto por userRootPath)
        // Pero por si acaso hay carpetas sin permisos en shared (que no deberían verse), el bucle asegura que solo las del usuario se añadan si no tienen permisos.
        foreach (var folder in allFolders)
        {
            if (!foldersWithPermissionsSet.Contains(folder.Id))
            {
                var normalizedPath = folder.Path.Replace('\\', '/').TrimEnd('/') + "/";
                if (normalizedPath.StartsWith(userRootPath.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))
                {
                    allowedPaths.Add(normalizedPath);
                }
            }
        }

        return allowedPaths;
    }
}

