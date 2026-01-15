using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Features.Timeline;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Folders;

public class FoldersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/folders")
            .WithTags("Folders")
            .RequireAuthorization();

        group.MapGet("", GetAllFolders)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/folders\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetAllFolders")
            .WithDescription("Gets all folders")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get all folders";
                operation.Description = "Returns a list of all folders registered in the database, including the count of assets in each folder.";
                return Task.CompletedTask;
            });

        group.MapGet("{folderId}", GetFolderById)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/folders/1\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetFolderById")
            .WithDescription("Gets a folder by ID")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get folder by ID";
                operation.Description = "Returns details of a specific folder, including its subfolders.";
                return Task.CompletedTask;
            });

        group.MapGet("{folderId}/assets", GetFolderAssets)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/folders/1/assets\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetFolderAssets")
            .WithDescription("Gets all assets in a folder")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get folder assets";
                operation.Description = "Returns a list of all media assets contained in a specific folder.";
                return Task.CompletedTask;
            });

        group.MapGet("tree", GetFolderTree)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/folders/tree\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetFolderTree")
            .WithDescription("Gets the complete folder tree structure")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get folder tree";
                operation.Description = "Returns the hierarchical structure of all folders, useful for tree-view navigation.";
                return Task.CompletedTask;
            });

        group.MapPost("", CreateFolder)
            .WithName("CreateFolder")
            .WithDescription("Creates a folder under the current user");

        group.MapPut("{folderId}", UpdateFolder)
            .WithName("UpdateFolder")
            .WithDescription("Updates a folder name or parent");

        group.MapDelete("{folderId}", DeleteFolder)
            .WithName("DeleteFolder")
            .WithDescription("Deletes a folder if it is empty");

        group.MapPost("assets/move", MoveFolderAssets)
            .WithName("MoveFolderAssets")
            .WithDescription("Moves assets between folders");
    }

    private async Task<IResult> GetAllFolders(
        [FromServices] ApplicationDbContext dbContext,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var isAdmin = user.IsInRole("Admin");
            var folders = await GetFoldersForUserAsync(dbContext, userId, isAdmin, includeAssets: true, cancellationToken);

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
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var folder = await dbContext.Folders
                .Include(f => f.Assets)
                .Include(f => f.SubFolders)
                .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

            if (folder == null)
            {
                return Results.NotFound(new { error = $"Folder with ID {folderId} not found" });
            }

            if (!await CanReadFolderAsync(dbContext, userId, user.IsInRole("Admin"), folderId, cancellationToken))
            {
                return Results.Forbid();
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
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var folder = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

            if (folder == null)
            {
                return Results.NotFound(new { error = $"Folder with ID {folderId} not found" });
            }

            if (!await CanReadFolderAsync(dbContext, userId, user.IsInRole("Admin"), folderId, cancellationToken))
            {
                return Results.Forbid();
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
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!TryGetUserId(user, out var userId))
            {
                return Results.Unauthorized();
            }

            var isAdmin = user.IsInRole("Admin");
            var allFolders = await GetFoldersForUserAsync(dbContext, userId, isAdmin, includeAssets: true, cancellationToken);

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

    private async Task<IResult> CreateFolder(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] SettingsService settingsService,
        [FromBody] CreateFolderRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "El nombre de la carpeta es obligatorio." });
        }

        Folder? parentFolder = null;
        if (request.ParentFolderId.HasValue)
        {
            parentFolder = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId.Value, cancellationToken);

            if (parentFolder == null)
            {
                return Results.NotFound(new { error = "Carpeta padre no encontrada." });
            }

            if (!await CanWriteFolderAsync(dbContext, userId, user.IsInRole("Admin"), parentFolder.Id, cancellationToken))
            {
                return Results.Forbid();
            }
        }

        var name = request.Name.Trim();
        var userRootPath = GetUserRootPath(userId);
        var normalizedPath = NormalizePath(parentFolder == null
            ? $"{userRootPath}/{name}"
            : $"{parentFolder.Path.TrimEnd('/')}/{name}");

        var physicalPath = await settingsService.ResolvePhysicalPathAsync(normalizedPath);
        if (Directory.Exists(physicalPath))
        {
            return Results.BadRequest(new { error = "Ya existe una carpeta con ese nombre en la ruta destino." });
        }

        Directory.CreateDirectory(physicalPath);

        var folder = new Folder
        {
            Name = name,
            Path = normalizedPath,
            ParentFolderId = parentFolder?.Id
        };

        dbContext.Folders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);

        var permissionExists = await dbContext.FolderPermissions
            .AnyAsync(p => p.UserId == userId && p.FolderId == folder.Id, cancellationToken);

        if (!permissionExists)
        {
            dbContext.FolderPermissions.Add(new FolderPermission
            {
                UserId = userId,
                FolderId = folder.Id,
                CanRead = true,
                CanWrite = true,
                CanDelete = true,
                CanManagePermissions = true,
                GrantedByUserId = userId
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var response = new FolderResponse
        {
            Id = folder.Id,
            Path = folder.Path,
            Name = folder.Name,
            ParentFolderId = folder.ParentFolderId,
            CreatedAt = folder.CreatedAt,
            AssetCount = 0
        };

        return Results.Ok(response);
    }

    private async Task<IResult> UpdateFolder(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] SettingsService settingsService,
        [FromRoute] int folderId,
        [FromBody] UpdateFolderRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var folder = await dbContext.Folders
            .Include(f => f.SubFolders)
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

        if (folder == null)
        {
            return Results.NotFound(new { error = "Carpeta no encontrada." });
        }

        if (!await CanWriteFolderAsync(dbContext, userId, user.IsInRole("Admin"), folderId, cancellationToken))
        {
            return Results.Forbid();
        }

        var newName = string.IsNullOrWhiteSpace(request.Name) ? folder.Name : request.Name.Trim();
        Folder? newParent = null;

        if (request.ParentFolderId.HasValue)
        {
            if (request.ParentFolderId.Value == folderId)
            {
                return Results.BadRequest(new { error = "La carpeta no puede ser su propio padre." });
            }

            newParent = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId.Value, cancellationToken);

            if (newParent == null)
            {
                return Results.NotFound(new { error = "Carpeta padre no encontrada." });
            }

            if (IsDescendantFolder(newParent, folderId, dbContext))
            {
                return Results.BadRequest(new { error = "No puedes mover una carpeta dentro de su propia subcarpeta." });
            }

            if (!await CanWriteFolderAsync(dbContext, userId, user.IsInRole("Admin"), newParent.Id, cancellationToken))
            {
                return Results.Forbid();
            }
        }

        var oldPath = folder.Path;
        var userRootPath = GetUserRootPath(userId);
        var newNormalizedPath = NormalizePath(newParent == null
            ? $"{userRootPath}/{newName}"
            : $"{newParent.Path.TrimEnd('/')}/{newName}");

        var oldPhysicalPath = await settingsService.ResolvePhysicalPathAsync(oldPath);
        var newPhysicalPath = await settingsService.ResolvePhysicalPathAsync(newNormalizedPath);

        if (!string.Equals(oldPhysicalPath, newPhysicalPath, StringComparison.OrdinalIgnoreCase))
        {
            var existingFolder = await dbContext.Folders
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Path == newNormalizedPath && f.Id != folderId, cancellationToken);
            if (existingFolder != null)
            {
                return Results.BadRequest(new { error = "Ya existe una carpeta con ese nombre en la ruta destino." });
            }

            if (Directory.Exists(newPhysicalPath))
            {
                return Results.BadRequest(new { error = "La carpeta destino ya existe." });
            }

            if (Directory.Exists(oldPhysicalPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newPhysicalPath)!);
                Directory.Move(oldPhysicalPath, newPhysicalPath);
            }
        }

        folder.Name = newName;
        folder.ParentFolderId = newParent?.Id;
        folder.Path = newNormalizedPath;

        await UpdateSubfolderPathsAsync(dbContext, folderId, oldPath, folder.Path, cancellationToken);
        await UpdateAssetPathsAsync(dbContext, oldPath, folder.Path, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new FolderResponse
        {
            Id = folder.Id,
            Path = folder.Path,
            Name = folder.Name,
            ParentFolderId = folder.ParentFolderId,
            CreatedAt = folder.CreatedAt,
            AssetCount = await dbContext.Assets.CountAsync(a => a.FolderId == folder.Id, cancellationToken)
        };

        return Results.Ok(response);
    }

    private async Task<IResult> DeleteFolder(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] SettingsService settingsService,
        [FromRoute] int folderId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var folder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);

        if (folder == null)
        {
            return Results.NotFound(new { error = "Carpeta no encontrada." });
        }

        if (!await CanDeleteFolderAsync(dbContext, userId, user.IsInRole("Admin"), folderId, cancellationToken))
        {
            return Results.Forbid();
        }

        var folderIdsToDelete = await GetFolderSubtreeIdsAsync(dbContext, folderId, cancellationToken);
        var hasAssets = await dbContext.Assets
            .AnyAsync(a => a.FolderId.HasValue && folderIdsToDelete.Contains(a.FolderId.Value), cancellationToken);

        if (hasAssets)
        {
            return Results.BadRequest(new { error = "La carpeta no puede contener archivos para poder eliminarla." });
        }

        var foldersToDelete = await dbContext.Folders
            .Where(f => folderIdsToDelete.Contains(f.Id))
            .ToListAsync(cancellationToken);

        dbContext.Folders.RemoveRange(foldersToDelete);
        var permissions = dbContext.FolderPermissions.Where(p => folderIdsToDelete.Contains(p.FolderId));
        dbContext.FolderPermissions.RemoveRange(permissions);
        await dbContext.SaveChangesAsync(cancellationToken);

        var physicalPath = await settingsService.ResolvePhysicalPathAsync(folder.Path);
        if (Directory.Exists(physicalPath))
        {
            Directory.Delete(physicalPath, recursive: true);
        }

        return Results.NoContent();
    }

    private async Task<IResult> MoveFolderAssets(
        [FromServices] ApplicationDbContext dbContext,
        [FromServices] SettingsService settingsService,
        [FromBody] MoveFolderAssetsRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        if (request.AssetIds == null || request.AssetIds.Count == 0)
        {
            return Results.BadRequest(new { error = "Debes seleccionar al menos un asset." });
        }

        if (!await CanWriteFolderAsync(dbContext, userId, user.IsInRole("Admin"), request.SourceFolderId, cancellationToken) ||
            !await CanWriteFolderAsync(dbContext, userId, user.IsInRole("Admin"), request.TargetFolderId, cancellationToken))
        {
            return Results.Forbid();
        }

        var assets = await dbContext.Assets
            .Where(a => request.AssetIds.Contains(a.Id) && a.FolderId == request.SourceFolderId)
            .ToListAsync(cancellationToken);

        var targetFolder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Id == request.TargetFolderId, cancellationToken);

        if (targetFolder == null)
        {
            return Results.NotFound(new { error = "Carpeta destino no encontrada." });
        }

        var targetPhysicalPath = await settingsService.ResolvePhysicalPathAsync(targetFolder.Path);
        Directory.CreateDirectory(targetPhysicalPath);

        foreach (var asset in assets)
        {
            var sourcePhysicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
            if (!File.Exists(sourcePhysicalPath))
            {
                continue;
            }

            var fileName = Path.GetFileName(sourcePhysicalPath);
            var newPhysicalPath = Path.Combine(targetPhysicalPath, fileName);
            if (File.Exists(newPhysicalPath))
            {
                var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
                newPhysicalPath = Path.Combine(targetPhysicalPath, uniqueName);
                fileName = uniqueName;
            }

            File.Move(sourcePhysicalPath, newPhysicalPath);

            asset.FolderId = request.TargetFolderId;
            asset.FileName = fileName;
            asset.FullPath = await settingsService.VirtualizePathAsync(newPhysicalPath);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        userId = 0;
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim.Value, out userId);
    }

    private static async Task<bool> CanReadFolderAsync(ApplicationDbContext dbContext, int userId, bool isAdmin, int folderId, CancellationToken ct)
    {
        if (isAdmin)
        {
            return true;
        }

        var hasPermissions = await dbContext.FolderPermissions
            .AnyAsync(p => p.FolderId == folderId, ct);

        if (!hasPermissions)
        {
            return true;
        }

        return await dbContext.FolderPermissions
            .AnyAsync(p => p.UserId == userId && p.FolderId == folderId && p.CanRead, ct);
    }

    private static async Task<bool> CanWriteFolderAsync(ApplicationDbContext dbContext, int userId, bool isAdmin, int folderId, CancellationToken ct)
    {
        if (isAdmin)
        {
            return true;
        }

        return await dbContext.FolderPermissions
            .AnyAsync(p => p.UserId == userId && p.FolderId == folderId && p.CanWrite, ct);
    }

    private static async Task<bool> CanDeleteFolderAsync(ApplicationDbContext dbContext, int userId, bool isAdmin, int folderId, CancellationToken ct)
    {
        if (isAdmin)
        {
            return true;
        }

        return await dbContext.FolderPermissions
            .AnyAsync(p => p.UserId == userId && p.FolderId == folderId && p.CanDelete, ct);
    }

    private static async Task<List<Folder>> GetFoldersForUserAsync(
        ApplicationDbContext dbContext,
        int userId,
        bool isAdmin,
        bool includeAssets,
        CancellationToken ct)
    {
        var query = dbContext.Folders.AsQueryable();
        if (includeAssets)
        {
            query = query.Include(f => f.Assets);
        }

        if (isAdmin)
        {
            return await query.ToListAsync(ct);
        }

        var allFolders = await query.ToListAsync(ct);
        var permissions = await dbContext.FolderPermissions.ToListAsync(ct);

        var foldersWithPermissions = permissions
            .Select(p => p.FolderId)
            .ToHashSet();

        var readableIds = permissions
            .Where(p => p.UserId == userId && p.CanRead)
            .Select(p => p.FolderId)
            .ToHashSet();

        var allowedIds = new HashSet<int>(readableIds);

        foreach (var folder in allFolders)
        {
            if (!foldersWithPermissions.Contains(folder.Id))
            {
                allowedIds.Add(folder.Id);
            }
        }

        AddAncestorFolders(allFolders, allowedIds);

        return allFolders.Where(f => allowedIds.Contains(f.Id)).ToList();
    }

    private static void AddAncestorFolders(List<Folder> allFolders, HashSet<int> allowedIds)
    {
        var lookup = allFolders.ToDictionary(f => f.Id, f => f);

        foreach (var folderId in allowedIds.ToList())
        {
            if (!lookup.TryGetValue(folderId, out var current))
            {
                continue;
            }

            while (current.ParentFolderId.HasValue)
            {
                var parentId = current.ParentFolderId.Value;
                if (!allowedIds.Contains(parentId))
                {
                    allowedIds.Add(parentId);
                }

                if (!lookup.TryGetValue(parentId, out var parent))
                {
                    break;
                }

                current = parent;
            }
        }
    }

    private static bool IsDescendantFolder(Folder potentialParent, int folderId, ApplicationDbContext dbContext)
    {
        var currentParentId = potentialParent.Id;
        while (true)
        {
            if (currentParentId == folderId)
            {
                return true;
            }

            var parent = dbContext.Folders.FirstOrDefault(f => f.Id == currentParentId);
            if (parent?.ParentFolderId == null)
            {
                return false;
            }

            currentParentId = parent.ParentFolderId.Value;
        }
    }

    private static async Task UpdateSubfolderPathsAsync(
        ApplicationDbContext dbContext,
        int folderId,
        string oldPath,
        string newPath,
        CancellationToken ct)
    {
        var subfolders = await dbContext.Folders
            .Where(f => f.ParentFolderId == folderId)
            .ToListAsync(ct);

        foreach (var subfolder in subfolders)
        {
            var childOldPath = subfolder.Path;
            subfolder.Path = $"{newPath.TrimEnd('/')}/{subfolder.Name}";
            await UpdateSubfolderPathsAsync(dbContext, subfolder.Id, childOldPath, subfolder.Path, ct);
        }
    }

    private static async Task UpdateAssetPathsAsync(
        ApplicationDbContext dbContext,
        string oldVirtualPath,
        string newVirtualPath,
        CancellationToken ct)
    {
        var normalizedOldPath = NormalizePath(oldVirtualPath);
        var normalizedNewPath = NormalizePath(newVirtualPath);
        var likePattern = $"{normalizedOldPath}%";

        var assets = await dbContext.Assets
            .Where(a => EF.Functions.ILike(a.FullPath, likePattern))
            .ToListAsync(ct);

        foreach (var asset in assets)
        {
            asset.FullPath = normalizedNewPath + asset.FullPath.Substring(normalizedOldPath.Length);
        }
    }

    private static async Task<HashSet<int>> GetFolderSubtreeIdsAsync(
        ApplicationDbContext dbContext,
        int folderId,
        CancellationToken ct)
    {
        var allFolders = await dbContext.Folders
            .AsNoTracking()
            .Select(f => new { f.Id, f.ParentFolderId })
            .ToListAsync(ct);

        var childrenLookup = allFolders
            .GroupBy(f => f.ParentFolderId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<int>();
        var stack = new Stack<int>();
        stack.Push(folderId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current))
            {
                continue;
            }

            if (childrenLookup.TryGetValue(current, out var children))
            {
                foreach (var childId in children)
                {
                    stack.Push(childId);
                }
            }
        }

        return result;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }

    private static string GetUserRootPath(int userId)
    {
        return $"/assets/users/{userId}";
    }
}

public class CreateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
}

public class UpdateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public int? ParentFolderId { get; set; }
}

public class MoveFolderAssetsRequest
{
    public int SourceFolderId { get; set; }
    public int TargetFolderId { get; set; }
    public List<int> AssetIds { get; set; } = new();
}
