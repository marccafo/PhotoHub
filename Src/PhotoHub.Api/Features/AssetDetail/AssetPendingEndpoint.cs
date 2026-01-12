using Microsoft.AspNetCore.Mvc;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.API.Features.AssetDetail;

public class AssetPendingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/pending/detail", HandleDetail)
            .WithName("GetPendingAssetDetail")
            .WithTags("Assets")
            .WithDescription("Gets detailed information about a pending asset from the filesystem");

        app.MapGet("/api/assets/pending/content", HandleContent)
            .WithName("GetPendingAssetContent")
            .WithTags("Assets")
            .WithDescription("Gets the original content of a pending asset (image or video)");
    }

    private async Task<IResult> HandleDetail(
        [FromQuery] string path,
        [FromServices] SettingsService settingsService,
        [FromServices] ExifExtractorService exifService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
            return Results.BadRequest("Path is required");

        var physicalPath = await settingsService.ResolvePhysicalPathAsync(path);
        var userAssetsPath = await settingsService.GetAssetsPathAsync();
        var internalAssetsPath = settingsService.GetInternalAssetsPath();
        
        // Determinar si el archivo está en el directorio del usuario o en el interno
        var normalizedPhysicalPath = Path.GetFullPath(physicalPath);
        var normalizedUserPath = Path.GetFullPath(userAssetsPath);
        var normalizedInternalPath = Path.GetFullPath(internalAssetsPath);
        
        AssetSyncStatus syncStatus;
        if (normalizedPhysicalPath.StartsWith(normalizedInternalPath, StringComparison.OrdinalIgnoreCase))
        {
            // El archivo está en el directorio interno, está copiado pero no indexado
            syncStatus = AssetSyncStatus.Copied;
        }
        else if (normalizedPhysicalPath.StartsWith(normalizedUserPath, StringComparison.OrdinalIgnoreCase))
        {
            // El archivo está en el directorio del usuario, está pendiente
            syncStatus = AssetSyncStatus.Pending;
        }
        else
        {
            return Results.Forbid();
        }

        if (!File.Exists(physicalPath))
            return Results.NotFound("File not found");

        var fileInfo = new FileInfo(physicalPath);
        var extension = Path.GetExtension(physicalPath).ToLowerInvariant();
        var type = GetAssetType(extension);

        var exif = await exifService.ExtractExifAsync(physicalPath, cancellationToken);

        var response = new AssetDetailResponse
        {
            Id = 0,
            FileName = fileInfo.Name,
            FullPath = path, // Mantener la ruta original (podría ser virtual) para el cliente
            FileSize = fileInfo.Length,
            CreatedDate = fileInfo.CreationTimeUtc,
            ModifiedDate = fileInfo.LastWriteTimeUtc,
            Extension = extension,
            ScannedAt = DateTime.MinValue,
            Type = type.ToString(),
            Checksum = string.Empty,
            HasExif = exif != null,
            HasThumbnails = false,
            SyncStatus = syncStatus,
            Exif = exif != null ? new ExifDataResponse
            {
                DateTaken = exif.DateTimeOriginal,
                CameraMake = exif.CameraMake,
                CameraModel = exif.CameraModel,
                Width = exif.Width,
                Height = exif.Height,
                Orientation = exif.Orientation,
                Latitude = exif.Latitude,
                Longitude = exif.Longitude,
                Altitude = exif.Altitude,
                Iso = exif.Iso,
                Aperture = exif.Aperture,
                ShutterSpeed = exif.ShutterSpeed,
                FocalLength = exif.FocalLength,
                Description = exif.Description,
                Keywords = exif.Keywords
            } : null
        };

        return Results.Ok(response);
    }

    private async Task<IResult> HandleContent(
        [FromQuery] string path,
        [FromServices] SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
            return Results.BadRequest("Path is required");

        var physicalPath = await settingsService.ResolvePhysicalPathAsync(path);
        var assetsPath = await settingsService.GetAssetsPathAsync();
        if (!IsPathSafe(physicalPath, assetsPath))
            return Results.Forbid();

        if (!File.Exists(physicalPath))
            return Results.NotFound("File not found");

        var extension = Path.GetExtension(physicalPath).ToLowerInvariant();
        var type = GetAssetType(extension);
        var contentType = GetContentType(extension, type);

        return Results.File(physicalPath, contentType, enableRangeProcessing: true);
    }

    private bool IsPathSafe(string path, string assetsPath)
    {
        var fullPath = Path.GetFullPath(path);
        var fullAssetsPath = Path.GetFullPath(assetsPath);
        return fullPath.StartsWith(fullAssetsPath, StringComparison.OrdinalIgnoreCase);
    }

    private AssetType GetAssetType(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp", ".heic", ".heif" };
        return imageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ? AssetType.IMAGE : AssetType.VIDEO;
    }

    private string GetContentType(string extension, AssetType type)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mkv" => "video/x-matroska",
            _ => type == AssetType.VIDEO ? "video/mp4" : "application/octet-stream"
        };
    }
}
