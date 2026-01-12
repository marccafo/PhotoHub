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
                SyncStatus = AssetSyncStatus.Synced
            }).ToList();

            // Detect non-indexed files in configured assets path
            var assetsPath = await settingsService.GetAssetsPathAsync();

            if (Directory.Exists(assetsPath))
            {
                Console.WriteLine($"[DEBUG] Scanning directory for new assets: {assetsPath}");
                var scannedFiles = (await directoryScanner.ScanDirectoryAsync(assetsPath, cancellationToken)).ToList();
                Console.WriteLine($"[DEBUG] Found {scannedFiles.Count} files in filesystem");
                
                // Normalizar rutas existentes para una comparación robusta
                var existingPaths = assets
                    .Select(a => a.FullPath.Replace('\\', '/').TrimEnd('/'))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                int pendingCount = 0;
                foreach (var file in scannedFiles)
                {
                    var normalizedFilePath = file.FullPath.Replace('\\', '/').TrimEnd('/');
                    
                    if (!existingPaths.Contains(normalizedFilePath))
                    {
                        pendingCount++;
                        timelineItems.Add(new TimelineResponse
                        {
                            Id = 0,
                            FileName = file.FileName,
                            FullPath = file.FullPath,
                            FileSize = file.FileSize,
                            CreatedDate = file.CreatedDate,
                            ModifiedDate = file.ModifiedDate,
                            Extension = file.Extension,
                            ScannedAt = DateTime.MinValue,
                            Type = file.AssetType.ToString(),
                            SyncStatus = AssetSyncStatus.Pending
                        });
                    }
                }
                Console.WriteLine($"[DEBUG] Identified {pendingCount} pending assets to show in timeline");
            }

            // Re-order by most recent date (preferring CreatedDate but handles cases where only ModifiedDate is available)
            var orderedTimeline = timelineItems
                .OrderByDescending(a => a.SyncStatus == AssetSyncStatus.Pending ? a.ModifiedDate : a.CreatedDate)
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

