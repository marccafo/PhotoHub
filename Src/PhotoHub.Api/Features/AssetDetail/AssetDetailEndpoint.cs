using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.AssetDetail;

public class AssetDetailEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/{assetId}", Handle)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/assets/1\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetAssetDetail")
            .WithTags("Assets")
            .WithDescription("Gets detailed information about an asset")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get asset details";
                operation.Description = "Returns detailed information about an asset including EXIF data, thumbnails, tags, and folder information.";
                return Task.CompletedTask;
            });
    }

    private async Task<IResult> Handle(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var asset = await dbContext.Assets
                .Include(a => a.Exif)
                .Include(a => a.Thumbnails)
                .Include(a => a.Tags)
                .Include(a => a.Folder)
                .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);

            if (asset == null)
            {
                return Results.NotFound(new { error = $"Asset with ID {assetId} not found" });
            }

            var response = new AssetDetailResponse
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
                FolderId = asset.FolderId,
                FolderPath = asset.Folder?.Path,
                Exif = asset.Exif != null ? new ExifDataResponse
                {
                    DateTaken = asset.Exif.DateTimeOriginal,
                    CameraMake = asset.Exif.CameraMake,
                    CameraModel = asset.Exif.CameraModel,
                    Width = asset.Exif.Width,
                    Height = asset.Exif.Height,
                    Orientation = asset.Exif.Orientation,
                    Latitude = asset.Exif.Latitude,
                    Longitude = asset.Exif.Longitude,
                    Software = null // Not available in AssetExif model
                } : null,
                Thumbnails = asset.Thumbnails.Select(t => new ThumbnailInfoResponse
                {
                    Id = t.Id,
                    Size = t.Size.ToString(),
                    Width = t.Width,
                    Height = t.Height,
                    AssetId = t.AssetId
                }).ToList(),
                Tags = asset.Tags.Select(t => t.TagType.ToString()).ToList()
            };

            return Results.Ok(response);
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
