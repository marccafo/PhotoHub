using Microsoft.AspNetCore.Mvc;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.ScanPhotos;

public static class ScanPhotosEndpoint
{
    public static void MapScanPhotosEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/photos/scan", Handle)
        .WithName("ScanPhotos")
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
        string directoryPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var scannedPhotos = await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken);
        
            var scannedAt = DateTime.UtcNow;
            var savedPhotos = new List<PhotoEntity>();
            
            var localTimeZone = TimeZoneInfo.Local;
            var timeZoneOffsetMinutes = (int)localTimeZone.GetUtcOffset(DateTime.Now).TotalMinutes;

            foreach (var photo in scannedPhotos)
            {
                var createdDateUtc = photo.CreatedDate.Kind == DateTimeKind.Utc 
                    ? photo.CreatedDate 
                    : photo.CreatedDate.ToUniversalTime();
            
                var modifiedDateUtc = photo.ModifiedDate.Kind == DateTimeKind.Utc 
                    ? photo.ModifiedDate 
                    : photo.ModifiedDate.ToUniversalTime();

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
                
                savedPhotos.Add(photoEntity);
            }
            
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

