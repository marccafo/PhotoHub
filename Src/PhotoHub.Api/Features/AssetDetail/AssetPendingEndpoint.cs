using Microsoft.AspNetCore.Mvc;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

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

        var assetsPath = await settingsService.GetAssetsPathAsync();
        if (!IsPathSafe(path, assetsPath))
            return Results.Forbid();

        if (!File.Exists(path))
            return Results.NotFound("File not found");

        var fileInfo = new FileInfo(path);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var type = GetAssetType(extension);

        var exif = await exifService.ExtractExifAsync(path, cancellationToken);

        var response = new AssetDetailResponse
        {
            Id = 0,
            FileName = fileInfo.Name,
            FullPath = fileInfo.FullName,
            FileSize = fileInfo.Length,
            CreatedDate = fileInfo.CreationTimeUtc,
            ModifiedDate = fileInfo.LastWriteTimeUtc,
            Extension = extension,
            ScannedAt = DateTime.MinValue,
            Type = type.ToString(),
            Checksum = string.Empty,
            HasExif = exif != null,
            HasThumbnails = false,
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

        var assetsPath = await settingsService.GetAssetsPathAsync();
        if (!IsPathSafe(path, assetsPath))
            return Results.Forbid();

        if (!File.Exists(path))
            return Results.NotFound("File not found");

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var type = GetAssetType(extension);
        var contentType = GetContentType(extension, type);

        return Results.File(path, contentType, enableRangeProcessing: true);
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
