using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Features.Timeline;

namespace PhotoHub.API.Features.Albums;

public class AlbumsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/albums", GetAllAlbums)
            .WithName("GetAllAlbums")
            .WithTags("Albums")
            .WithDescription("Gets all albums");

        app.MapGet("/api/albums/{albumId}", GetAlbumById)
            .WithName("GetAlbumById")
            .WithTags("Albums")
            .WithDescription("Gets an album by ID");

        app.MapGet("/api/albums/{albumId}/assets", GetAlbumAssets)
            .WithName("GetAlbumAssets")
            .WithTags("Albums")
            .WithDescription("Gets all assets in an album");

        app.MapPost("/api/albums", CreateAlbum)
            .WithName("CreateAlbum")
            .WithTags("Albums")
            .WithDescription("Creates a new album");

        app.MapPut("/api/albums/{albumId}", UpdateAlbum)
            .WithName("UpdateAlbum")
            .WithTags("Albums")
            .WithDescription("Updates an album");

        app.MapDelete("/api/albums/{albumId}", DeleteAlbum)
            .WithName("DeleteAlbum")
            .WithTags("Albums")
            .WithDescription("Deletes an album");

        app.MapPost("/api/albums/{albumId}/assets", AddAssetToAlbum)
            .WithName("AddAssetToAlbum")
            .WithTags("Albums")
            .WithDescription("Adds an asset to an album");

        app.MapDelete("/api/albums/{albumId}/assets/{assetId}", RemoveAssetFromAlbum)
            .WithName("RemoveAssetFromAlbum")
            .WithTags("Albums")
            .WithDescription("Removes an asset from an album");

        app.MapPut("/api/albums/{albumId}/cover", SetAlbumCover)
            .WithName("SetAlbumCover")
            .WithTags("Albums")
            .WithDescription("Sets the cover image for an album");
    }

    private async Task<IResult> GetAllAlbums(
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var albums = await dbContext.Albums
                .Include(a => a.AlbumAssets)
                    .ThenInclude(aa => aa.Asset)
                        .ThenInclude(asset => asset.Thumbnails)
                .Include(a => a.CoverAsset)
                    .ThenInclude(ca => ca!.Thumbnails)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync(cancellationToken);

            var response = albums.Select(a => new AlbumResponse
            {
                Id = a.Id,
                Name = a.Name,
                Description = a.Description,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                AssetCount = a.AlbumAssets.Count,
                CoverThumbnailUrl = a.CoverAsset?.Thumbnails
                    .FirstOrDefault(t => t.Size == ThumbnailSize.Medium) != null
                    ? $"/api/assets/{a.CoverAssetId}/thumbnail?size=Medium"
                    : a.AlbumAssets.OrderBy(aa => aa.Order).FirstOrDefault()?.Asset != null
                        ? $"/api/assets/{a.AlbumAssets.OrderBy(aa => aa.Order).First().AssetId}/thumbnail?size=Medium"
                        : null
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] GetAllAlbums: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> GetAlbumById(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        CancellationToken cancellationToken)
    {
        try
        {
            var album = await dbContext.Albums
                .Include(a => a.AlbumAssets)
                    .ThenInclude(aa => aa.Asset)
                        .ThenInclude(asset => asset.Thumbnails)
                .Include(a => a.CoverAsset)
                    .ThenInclude(ca => ca!.Thumbnails)
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            string? coverUrl = null;
            if (album.CoverAssetId.HasValue && album.CoverAsset?.Thumbnails.Any(t => t.Size == ThumbnailSize.Medium) == true)
            {
                coverUrl = $"/api/assets/{album.CoverAssetId}/thumbnail?size=Medium";
            }
            else if (album.AlbumAssets.Any())
            {
                var firstAsset = album.AlbumAssets.OrderBy(aa => aa.Order).First().Asset;
                if (firstAsset?.Thumbnails.Any(t => t.Size == ThumbnailSize.Medium) == true)
                {
                    coverUrl = $"/api/assets/{firstAsset.Id}/thumbnail?size=Medium";
                }
            }

            var response = new AlbumResponse
            {
                Id = album.Id,
                Name = album.Name,
                Description = album.Description,
                CreatedAt = album.CreatedAt,
                UpdatedAt = album.UpdatedAt,
                AssetCount = album.AlbumAssets.Count,
                CoverThumbnailUrl = coverUrl
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] GetAlbumById: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> GetAlbumAssets(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        CancellationToken cancellationToken)
    {
        try
        {
            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            var albumAssets = await dbContext.AlbumAssets
                .Include(aa => aa.Asset)
                    .ThenInclude(a => a.Exif)
                .Include(aa => aa.Asset)
                    .ThenInclude(a => a.Thumbnails)
                .Where(aa => aa.AlbumId == albumId)
                .OrderBy(aa => aa.Order)
                .ThenBy(aa => aa.AddedAt)
                .ToListAsync(cancellationToken);

            var response = albumAssets.Select(aa => new TimelineResponse
            {
                Id = aa.Asset.Id,
                FileName = aa.Asset.FileName,
                FullPath = aa.Asset.FullPath,
                FileSize = aa.Asset.FileSize,
                CreatedDate = aa.Asset.CreatedDate,
                ModifiedDate = aa.Asset.ModifiedDate,
                Extension = aa.Asset.Extension,
                ScannedAt = aa.Asset.ScannedAt,
                Type = aa.Asset.Type.ToString(),
                Checksum = aa.Asset.Checksum,
                HasExif = aa.Asset.Exif != null,
                HasThumbnails = aa.Asset.Thumbnails.Any(),
                SyncStatus = PhotoHub.Blazor.Shared.Models.AssetSyncStatus.Synced
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] GetAlbumAssets: {ex.Message}");
            Console.WriteLine(ex.StackTrace);

            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> CreateAlbum(
        [FromServices] ApplicationDbContext dbContext,
        [FromBody] CreateAlbumRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Album name is required" });
            }

            var album = new Album
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Albums.Add(album);
            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new AlbumResponse
            {
                Id = album.Id,
                Name = album.Name,
                Description = album.Description,
                CreatedAt = album.CreatedAt,
                UpdatedAt = album.UpdatedAt,
                AssetCount = 0
            };

            return Results.Created($"/api/albums/{album.Id}", response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] CreateAlbum: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> UpdateAlbum(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        [FromBody] UpdateAlbumRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Album name is required" });
            }

            album.Name = request.Name.Trim();
            album.Description = request.Description?.Trim();
            album.UpdatedAt = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);

            var response = new AlbumResponse
            {
                Id = album.Id,
                Name = album.Name,
                Description = album.Description,
                CreatedAt = album.CreatedAt,
                UpdatedAt = album.UpdatedAt,
                AssetCount = await dbContext.AlbumAssets.CountAsync(aa => aa.AlbumId == albumId, cancellationToken)
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] UpdateAlbum: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> DeleteAlbum(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        CancellationToken cancellationToken)
    {
        try
        {
            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            dbContext.Albums.Remove(album);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] DeleteAlbum: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> AddAssetToAlbum(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        [FromBody] AddAssetRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return Results.BadRequest(new { error = "Request body is required" });
            }

            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            var asset = await dbContext.Assets
                .FirstOrDefaultAsync(a => a.Id == request.AssetId, cancellationToken);

            if (asset == null)
            {
                return Results.NotFound(new { error = $"Asset with ID {request.AssetId} not found" });
            }

            // Check if asset is already in album
            var existing = await dbContext.AlbumAssets
                .AnyAsync(aa => aa.AlbumId == albumId && aa.AssetId == request.AssetId, cancellationToken);

            if (existing)
            {
                return Results.BadRequest(new { error = "Asset is already in this album" });
            }

            // Get the next order value
            var maxOrder = 0;
            if (await dbContext.AlbumAssets.AnyAsync(aa => aa.AlbumId == albumId, cancellationToken))
            {
                maxOrder = await dbContext.AlbumAssets
                    .Where(aa => aa.AlbumId == albumId)
                    .MaxAsync(aa => aa.Order, cancellationToken);
            }

            var albumAsset = new AlbumAsset
            {
                AlbumId = albumId,
                AssetId = request.AssetId,
                Order = maxOrder + 1,
                AddedAt = DateTime.UtcNow
            };

            dbContext.AlbumAssets.Add(albumAsset);

            // If album has no cover, set this as cover
            if (album.CoverAssetId == null)
            {
                album.CoverAssetId = request.AssetId;
            }

            album.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Asset added to album successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] AddAssetToAlbum: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> RemoveAssetFromAlbum(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        [FromRoute] int assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var albumAsset = await dbContext.AlbumAssets
                .FirstOrDefaultAsync(aa => aa.AlbumId == albumId && aa.AssetId == assetId, cancellationToken);

            if (albumAsset == null)
            {
                return Results.NotFound(new { error = "Asset not found in this album" });
            }

            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album != null)
            {
                // If this was the cover asset, clear it
                if (album.CoverAssetId == assetId)
                {
                    album.CoverAssetId = null;
                }
                album.UpdatedAt = DateTime.UtcNow;
            }

            dbContext.AlbumAssets.Remove(albumAsset);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] RemoveAssetFromAlbum: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private async Task<IResult> SetAlbumCover(
        [FromServices] ApplicationDbContext dbContext,
        [FromRoute] int albumId,
        [FromBody] SetCoverRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return Results.BadRequest(new { error = "Request body is required" });
            }

            var album = await dbContext.Albums
                .FirstOrDefaultAsync(a => a.Id == albumId, cancellationToken);

            if (album == null)
            {
                return Results.NotFound(new { error = $"Album with ID {albumId} not found" });
            }

            // Verify asset is in the album
            var albumAsset = await dbContext.AlbumAssets
                .AnyAsync(aa => aa.AlbumId == albumId && aa.AssetId == request.AssetId, cancellationToken);

            if (!albumAsset)
            {
                return Results.BadRequest(new { error = "Asset must be in the album to set it as cover" });
            }

            album.CoverAssetId = request.AssetId;
            album.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Cover image updated successfully" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] SetAlbumCover: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}

public class AlbumResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int AssetCount { get; set; }
    public string? CoverThumbnailUrl { get; set; }
}

public class CreateAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddAssetRequest
{
    public int AssetId { get; set; }
}

public class SetCoverRequest
{
    public int AssetId { get; set; }
}
