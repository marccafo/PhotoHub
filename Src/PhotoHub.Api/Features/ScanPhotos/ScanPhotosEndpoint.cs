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

    private static async Task<IResult> Handle(
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

            foreach (var photo in scannedPhotos)
            {
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
    
    private static DateTime ConvertUtcToLocal(DateTime utcDateTime, int offsetMinutes)
    {
        return utcDateTime.AddMinutes(offsetMinutes);
    }
}

