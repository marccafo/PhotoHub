using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.Thumbnails;

public class ThumbnailEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/{assetId}/thumbnail", Handle)
            .WithName("GetThumbnail")
            .WithTags("Assets")
            .WithDescription("Gets a thumbnail for an asset")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get asset thumbnail";
                operation.Description = "Returns a thumbnail image file for the specified asset. Supports Small (220px), Medium (640px), and Large (1280px) sizes.";
                return Task.CompletedTask;
            });
    }

    private async Task<IResult> Handle(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] ThumbnailGeneratorService thumbnailService,
        [FromRoute] int assetId,
        [FromQuery] string size = "Medium",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate asset exists
            var asset = await dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);

            if (asset == null)
            {
                return Results.NotFound(new { error = $"Asset with ID {assetId} not found" });
            }

            // Parse size
            if (!Enum.TryParse<ThumbnailSize>(size, true, out var thumbnailSize))
            {
                thumbnailSize = ThumbnailSize.Medium;
            }

            // Get thumbnail from database
            var thumbnail = await dbContext.AssetThumbnails
                .FirstOrDefaultAsync(t => t.AssetId == assetId && t.Size == thumbnailSize, cancellationToken);

            if (thumbnail == null)
            {
                return Results.NotFound(new { error = $"Thumbnail not found for asset {assetId} with size {size}" });
            }

            // Check if file exists
            if (!File.Exists(thumbnail.FilePath))
            {
                return Results.NotFound(new { error = $"Thumbnail file not found at path: {thumbnail.FilePath}" });
            }

            // Return file
            var fileBytes = await File.ReadAllBytesAsync(thumbnail.FilePath, cancellationToken);
            var contentType = thumbnail.Format == "WebP" ? "image/webp" : "image/jpeg";

            return Results.File(fileBytes, contentType, $"{asset.FileName}_thumb_{size}.jpg");
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
