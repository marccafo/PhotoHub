using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.ScanPhotos;

public class ScanPhotosEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/photos/scan", Handle)
        .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/photos/scan?directoryPath=C:\\test-photos\" -H \"Accept: application/json\"",
                label: "cURL Example")
        .WithName("ScanPhotos")
        .WithTags("Photos")
        .WithDescription("cans a directory and returns the list of found photographs")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Scans a photo directory";
            operation.Description = "This endpoint recursively scans a specified directory and returns a list of all found photographs. Only files with extensions: .jpg, .jpeg, .png, .bmp, .tiff are included";
            return Task.CompletedTask;
        });
    }

    private async Task<IResult> Handle(
        [FromServices] DirectoryScanner directoryScanner,
        [FromServices] PhotoDbContext dbContext,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var scannedPhotos = await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken);
        
            var scannedAt = DateTime.UtcNow;
            var savedPhotos = new List<PhotoEntity>();
            
            // Obtener el offset del timezone local actual
            var localTimeZone = TimeZoneInfo.Local;
            var timeZoneOffsetMinutes = (int)localTimeZone.GetUtcOffset(DateTime.Now).TotalMinutes;

            // Track unique directories to avoid duplicate folder creation
            var processedDirectories = new HashSet<string>();

            foreach (var photo in scannedPhotos)
            {
                // Ensure folder structure exists for this photo's directory
                var photoDirectory = Path.GetDirectoryName(photo.FullPath);
                if (!string.IsNullOrEmpty(photoDirectory) && !processedDirectories.Contains(photoDirectory))
                {
                    await EnsureFolderStructureExistsAsync(dbContext, photoDirectory, cancellationToken);
                    processedDirectories.Add(photoDirectory);
                }

                // Verificar si la foto ya existe en la BD por FullPath
                var existingPhoto = await dbContext.Photos
                    .FirstOrDefaultAsync(p => p.FullPath == photo.FullPath, cancellationToken);

                // Convertir fechas locales a UTC
                var createdDateUtc = photo.CreatedDate.Kind == DateTimeKind.Utc 
                    ? photo.CreatedDate 
                    : photo.CreatedDate.ToUniversalTime();
                
                var modifiedDateUtc = photo.ModifiedDate.Kind == DateTimeKind.Utc 
                    ? photo.ModifiedDate 
                    : photo.ModifiedDate.ToUniversalTime();

                if (existingPhoto == null)
                {
                    // Crear nueva entidad
                    var photoEntity = new PhotoEntity
                    {
                        FileName = photo.FileName,
                        FullPath = photo.FullPath,
                        FileSize = photo.FileSize,
                        CreatedDate = createdDateUtc,
                        ModifiedDate = modifiedDateUtc,
                        Extension = photo.Extension,
                        ScannedAt = scannedAt,
                        TimeZoneOffsetMinutes = timeZoneOffsetMinutes
                    };
                    
                    dbContext.Photos.Add(photoEntity);
                    savedPhotos.Add(photoEntity);
                }
                else
                {
                    // Actualizar información si el archivo ha cambiado
                    if (existingPhoto.ModifiedDate != modifiedDateUtc || 
                        existingPhoto.FileSize != photo.FileSize)
                    {
                        existingPhoto.FileSize = photo.FileSize;
                        existingPhoto.ModifiedDate = modifiedDateUtc;
                        existingPhoto.ScannedAt = scannedAt;
                        // Actualizar el offset si cambió (por si se escanea desde otra zona horaria)
                        existingPhoto.TimeZoneOffsetMinutes = timeZoneOffsetMinutes;
                        savedPhotos.Add(existingPhoto);
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Convertir de UTC a local al devolver
            var photos = savedPhotos.Select(photo => new ScanPhotosResponse
            {
                FileName = photo.FileName,
                FullPath = photo.FullPath,
                FileSize = photo.FileSize,
                CreatedDate = ConvertUtcToLocal(photo.CreatedDate, photo.TimeZoneOffsetMinutes),
                ModifiedDate = ConvertUtcToLocal(photo.ModifiedDate, photo.TimeZoneOffsetMinutes),
                Extension = photo.Extension
            });
            
            return Results.Ok(photos);
        }
        catch (DirectoryNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
    
    private DateTime ConvertUtcToLocal(DateTime utcDateTime, int offsetMinutes)
    {
        return utcDateTime.AddMinutes(offsetMinutes);
    }

    /// <summary>
    /// Ensures that the folder structure exists in the database, creating all parent folders if necessary
    /// </summary>
    private async Task EnsureFolderStructureExistsAsync(
        PhotoDbContext dbContext,
        string folderPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(folderPath))
            return;

        // Normalize path: convert backslashes to forward slashes and remove trailing slashes
        // Preserve Windows drive letters (C:, D:, etc.)
        var normalizedPath = folderPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedPath))
            return;

        // Check if folder already exists
        var existingFolder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Path == normalizedPath, cancellationToken);

        if (existingFolder != null)
            return;

        // Get parent directory using Path.GetDirectoryName (works with both / and \)
        var parentPath = Path.GetDirectoryName(folderPath);
        Folder? parentFolder = null;

        // Recursively ensure parent folder exists
        if (!string.IsNullOrEmpty(parentPath) && parentPath != folderPath)
        {
            // Normalize parent path
            var normalizedParentPath = parentPath.Replace('\\', '/').TrimEnd('/');
            
            await EnsureFolderStructureExistsAsync(dbContext, normalizedParentPath, cancellationToken);
            
            // Get the parent folder after ensuring it exists
            parentFolder = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Path == normalizedParentPath, cancellationToken);
        }

        // Extract folder name from original path (Path.GetFileName handles both separators)
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(folderName))
        {
            // If no name can be extracted, use the last part of normalized path
            folderName = normalizedPath.Split('/').LastOrDefault() ?? normalizedPath;
        }

        // Create the folder
        var folder = new Folder
        {
            Path = normalizedPath,
            Name = folderName,
            ParentFolderId = parentFolder?.Id,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Folders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

