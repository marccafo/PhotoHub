using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.UploadAssets;

public class UploadAssetsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/assets/upload", Handle)
            .DisableAntiforgery()
            .WithName("UploadAsset")
            .WithTags("Assets")
            .WithDescription("Uploads an asset to the internal storage and indexes it");
    }

    private async Task<IResult> Handle(
        [FromForm] IFormFile file,
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] FileHashService hashService,
        [FromServices] ExifExtractorService exifService,
        [FromServices] ThumbnailGeneratorService thumbnailService,
        [FromServices] MediaRecognitionService mediaRecognitionService,
        [FromServices] IMlJobService mlJobService,
        [FromServices] SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file uploaded");

        // Determinar la ruta interna de la biblioteca (Managed Library) - siempre usar la ruta del NAS
        var managedLibraryPath = settingsService.GetInternalAssetsPath();

        if (!Directory.Exists(managedLibraryPath))
            Directory.CreateDirectory(managedLibraryPath);

        // 1. Save file to temporary location to calculate hash
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            // 2. Calculate hash
            var checksum = await hashService.CalculateFileHashAsync(tempPath, cancellationToken);

            // 3. Check if it already exists
            var existingAsset = await dbContext.Assets.FirstOrDefaultAsync(a => a.Checksum == checksum, cancellationToken);
            if (existingAsset != null)
            {
                File.Delete(tempPath);
                return Results.Ok(new { message = "Asset already exists", assetId = existingAsset.Id });
            }

            // 4. Move to final destination (Managed Library)
            var finalFileName = file.FileName;
            var targetPath = Path.Combine(managedLibraryPath, finalFileName);

            // Handle filename collisions
            if (File.Exists(targetPath))
            {
                finalFileName = $"{Guid.NewGuid()}_{file.FileName}";
                targetPath = Path.Combine(managedLibraryPath, finalFileName);
            }

            File.Move(tempPath, targetPath);

            // 5. Create Asset record
            var fileInfo = new FileInfo(targetPath);
            var extension = Path.GetExtension(targetPath).ToLowerInvariant();
            var assetType = GetAssetType(extension);

            // Normalizar FullPath para la BD: si está en la biblioteca gestionada, usamos el prefijo /assets
            var dbPath = await settingsService.VirtualizePathAsync(targetPath);

            var asset = new Asset
            {
                FileName = finalFileName,
                FullPath = dbPath,
                FileSize = fileInfo.Length,
                Checksum = checksum,
                Type = assetType,
                Extension = extension,
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = fileInfo.LastWriteTimeUtc,
                ScannedAt = DateTime.UtcNow
            };

            // 6. Extract Metadata & Recognition
            var exif = await exifService.ExtractExifAsync(targetPath, cancellationToken);
            if (exif != null)
            {
                asset.Exif = exif;
                var tags = await mediaRecognitionService.DetectMediaTypeAsync(targetPath, exif, cancellationToken);
                if (tags.Any())
                {
                    asset.Tags = tags.Select(t => new AssetTag { TagType = t, DetectedAt = DateTime.UtcNow }).ToList();
                }
            }

            // 7. Save to DB
            dbContext.Assets.Add(asset);
            await dbContext.SaveChangesAsync(cancellationToken);

            // 8. Generate Thumbnails
            var thumbnails = await thumbnailService.GenerateThumbnailsAsync(targetPath, asset.Id, cancellationToken);
            if (thumbnails.Any())
            {
                dbContext.AssetThumbnails.AddRange(thumbnails);
            }

            // 9. Queue ML Jobs
            if (mediaRecognitionService.ShouldTriggerMlJob(asset, asset.Exif))
            {
                await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.FaceDetection, cancellationToken);
                await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.ObjectRecognition, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Asset uploaded successfully", assetId = asset.Id });
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            return Results.Problem(ex.Message);
        }
    }

    private AssetType GetAssetType(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp", ".heic", ".heif" };
        return imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? AssetType.IMAGE : AssetType.VIDEO;
    }
}
