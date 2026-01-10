using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Features.Timeline;

namespace PhotoHub.API.Features.Folders;

public class FoldersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/folders", GetAllFolders)
            .WithName("GetAllFolders")
            .WithTags("Folders")
            .WithDescription("Gets all folders");

        app.MapGet("/api/folders/{folderId}", GetFolderById)
            .WithName("GetFolderById")
            .WithTags("Folders")
            .WithDescription("Gets a folder by ID");

        app.MapGet("/api/folders/{folderId}/assets", GetFolderAssets)
            .WithName("GetFolderAssets")
            .WithTags("Folders")
            .WithDescription("Gets all assets in a folder");

        app.MapGet("/api/folders/tree", GetFolderTree)
            .WithName("GetFolderTree")
            .WithTags("Folders")
            .WithDescription("Gets the complete folder tree structure");
    }

    private async Task<IResult> GetAllFolders(
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var folders = await dbContext.Folders
                .Include(f => f.Assets)
                .ToListAsync(cancellationToken);

            var response = folders.Select(f => new FolderResponse
            {
                Id = f.Id,
                Path = f.Path,
                Name = f.Name,
                ParentFolderId = f.ParentFolderId,
                CreatedAt = f.CreatedAt,
                AssetCount = f.Assets.Count
            }).ToList();

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

    private async Task<IResult> GetFolderById(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int folderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = await dbContext.Folders
                .Include(f => f.Assets)
                .Include(f => f.SubFolders)
                .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

            if (folder == null)
            {
                return Results.NotFound(new { error = $"Folder with ID {folderId} not found" });
            }

            var response = new FolderResponse
            {
                Id = folder.Id,
                Path = folder.Path,
                Name = folder.Name,
                ParentFolderId = folder.ParentFolderId,
                CreatedAt = folder.CreatedAt,
                AssetCount = folder.Assets.Count,
                SubFolders = folder.SubFolders.Select(sf => new FolderResponse
                {
                    Id = sf.Id,
                    Path = sf.Path,
                    Name = sf.Name,
                    ParentFolderId = sf.ParentFolderId,
                    CreatedAt = sf.CreatedAt,
                    AssetCount = 0 // Don't load assets for subfolders in this query
                }).ToList()
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

    private async Task<IResult> GetFolderAssets(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int folderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var folder = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

            if (folder == null)
            {
                return Results.NotFound(new { error = $"Folder with ID {folderId} not found" });
            }

            var assets = await dbContext.Assets
                .Include(a => a.Exif)
                .Include(a => a.Thumbnails)
                .Where(a => a.FolderId == folderId)
                .OrderByDescending(a => a.ScannedAt)
                .ThenByDescending(a => a.ModifiedDate)
                .ToListAsync(cancellationToken);

            var response = assets.Select(asset => new TimelineResponse
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
                HasThumbnails = asset.Thumbnails.Any()
            }).ToList();

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

    private async Task<IResult> GetFolderTree(
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var allFolders = await dbContext.Folders
                .Include(f => f.Assets)
                .ToListAsync(cancellationToken);

            // Build tree structure
            var folderDict = allFolders.ToDictionary(f => f.Id, f => new FolderResponse
            {
                Id = f.Id,
                Path = f.Path,
                Name = f.Name,
                ParentFolderId = f.ParentFolderId,
                CreatedAt = f.CreatedAt,
                AssetCount = f.Assets.Count,
                SubFolders = new List<FolderResponse>()
            });

            var rootFolders = new List<FolderResponse>();

            foreach (var folder in folderDict.Values)
            {
                if (folder.ParentFolderId.HasValue && folderDict.ContainsKey(folder.ParentFolderId.Value))
                {
                    folderDict[folder.ParentFolderId.Value].SubFolders.Add(folder);
                }
                else
                {
                    rootFolders.Add(folder);
                }
            }

            return Results.Ok(rootFolders);
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
