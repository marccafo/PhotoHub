using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;
using System.Diagnostics;

namespace PhotoHub.API.Features.ScanAssets;

public class ScanAssetsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/scan", Handle)
        .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/assets/scan?directoryPath=C:\\Photos\" -H \"Accept: application/json\"",
                label: "cURL Example")
        .WithName("ScanAssets")
        .WithTags("Assets")
        .WithDescription("Scans a directory and returns the list of found media files (images and videos)")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Scans a media directory";
            operation.Description = "This endpoint recursively scans a specified directory, extracts metadata, generates thumbnails, and updates the database with all found media files. Supports images (JPG, PNG, etc.) and videos (MP4, AVI, etc.)";
            return Task.CompletedTask;
        });
    }

    private async Task<IResult> Handle(
        [FromServices] DirectoryScanner directoryScanner,
        [FromServices] FileHashService hashService,
        [FromServices] ExifExtractorService exifService,
        [FromServices] ThumbnailGeneratorService thumbnailService,
        [FromServices] ApplicationDbContext dbContext,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var scanStartTime = DateTime.UtcNow;
        var stats = new ScanStatistics();
        
        try
        {
            // STEP 1: Recursive file discovery
            var scannedFiles = await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken);
            stats.TotalFilesFound = scannedFiles.Count();
            
            // Track processed directories for folder structure creation
            var processedDirectories = new HashSet<string>();
            
            // Get all existing assets by checksum for differential comparison
            var existingAssetsByChecksum = await dbContext.Assets
                .ToDictionaryAsync(a => a.Checksum, a => a, cancellationToken);
            
            var existingAssetsByPath = await dbContext.Assets
                .ToDictionaryAsync(a => a.FullPath, a => a, cancellationToken);
            
            var assetsToCreate = new List<Asset>();
            var assetsToUpdate = new List<Asset>();
            var assetsToDelete = new HashSet<string>(); // Track paths that should exist
            var allScannedAssets = new HashSet<Asset>(); // Track all assets found during scan (for thumbnail verification)
            
            // Process each file
            foreach (var file in scannedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                assetsToDelete.Add(file.FullPath);
                
                // Ensure folder structure exists
                var fileDirectory = Path.GetDirectoryName(file.FullPath);
                if (!string.IsNullOrEmpty(fileDirectory) && !processedDirectories.Contains(fileDirectory))
                {
                    await EnsureFolderStructureExistsAsync(dbContext, fileDirectory, cancellationToken);
                    processedDirectories.Add(fileDirectory);
                }
                
                // STEP 2: Change verification (differential) - Quick heuristic first
                var existingByPath = existingAssetsByPath.GetValueOrDefault(file.FullPath);
                var needsFullCheck = existingByPath == null || 
                    hashService.HasFileChanged(file.FullPath, existingByPath.FileSize, existingByPath.ModifiedDate);
                
                if (!needsFullCheck)
                {
                    // File hasn't changed, but we still need to track it for thumbnail verification
                    if (existingByPath != null)
                    {
                        allScannedAssets.Add(existingByPath);
                    }
                    stats.SkippedUnchanged++;
                    continue;
                }
                
                // Calculate hash for change detection
                var checksum = await hashService.CalculateFileHashAsync(file.FullPath, cancellationToken);
                stats.HashesCalculated++;
                
                // Check if asset exists by checksum (handles moved/renamed files)
                var existingByChecksum = existingAssetsByChecksum.GetValueOrDefault(checksum);
                
                Asset? asset;
                bool isNew = false;
                
                if (existingByChecksum != null && existingByChecksum.FullPath != file.FullPath)
                {
                    // File was moved/renamed - update path
                    existingByChecksum.FullPath = file.FullPath;
                    existingByChecksum.FileName = file.FileName;
                    existingByChecksum.ModifiedDate = file.ModifiedDate;
                    existingByChecksum.FileSize = file.FileSize;
                    asset = existingByChecksum;
                    stats.MovedFiles++;
                }
                else if (existingByPath != null)
                {
                    // File exists at same path - update if changed
                    existingByPath.Checksum = checksum;
                    existingByPath.FileSize = file.FileSize;
                    existingByPath.ModifiedDate = file.ModifiedDate;
                    asset = existingByPath;
                    stats.UpdatedFiles++;
                }
                else
                {
                    // New file
                    var folder = await GetOrCreateFolderForPathAsync(dbContext, fileDirectory, cancellationToken);
                    
                    asset = new Asset
                    {
                        FileName = file.FileName,
                        FullPath = file.FullPath,
                        FileSize = file.FileSize,
                        Checksum = checksum,
                        Type = file.AssetType,
                        Extension = file.Extension,
                        CreatedDate = file.CreatedDate,
                        ModifiedDate = file.ModifiedDate,
                        ScannedAt = scanStartTime,
                        FolderId = folder?.Id
                    };
                    
                    assetsToCreate.Add(asset);
                    isNew = true;
                    stats.NewFiles++;
                }
                
                // STEP 3: Extract EXIF metadata (only for images) - will be linked after SaveChanges
                AssetExif? extractedExif = null;
                if (asset.Type == AssetType.IMAGE && isNew)
                {
                    extractedExif = await exifService.ExtractExifAsync(file.FullPath, cancellationToken);
                    if (extractedExif != null)
                    {
                        stats.ExifExtracted++;
                    }
                }
                
                // Store exif for later linking (after asset has Id)
                if (extractedExif != null && isNew)
                {
                    asset.Exif = extractedExif;
                }
                
                if (!isNew)
                {
                    assetsToUpdate.Add(asset);
                    allScannedAssets.Add(asset);
                }
            }
            
            // STEP 5: Insert/Update in database (atomic transaction)
            using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Add new assets
                if (assetsToCreate.Any())
                {
                    dbContext.Assets.AddRange(assetsToCreate);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    // STEP 4: Generate thumbnails for new assets (need asset.Id)
                    foreach (var asset in assetsToCreate.Where(a => a.Type == AssetType.IMAGE))
                    {
                        var thumbnails = await thumbnailService.GenerateThumbnailsAsync(
                            asset.FullPath, 
                            asset.Id, 
                            cancellationToken);
                        
                        if (thumbnails.Any())
                        {
                            dbContext.AssetThumbnails.AddRange(thumbnails);
                            stats.ThumbnailsGenerated += thumbnails.Count;
                        }
                    }
                    
                    // Save thumbnails for new assets
                    if (assetsToCreate.Any(a => a.Type == AssetType.IMAGE))
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                
                // Update existing assets
                if (assetsToUpdate.Any())
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    
                    // STEP 4b: Regenerate missing thumbnails for existing assets
                    foreach (var asset in assetsToUpdate.Where(a => a.Type == AssetType.IMAGE))
                    {
                        // Load thumbnails from database
                        await dbContext.Entry(asset)
                            .Collection(a => a.Thumbnails)
                            .LoadAsync(cancellationToken);
                        
                        // Check which thumbnails are missing physically
                        var missingSizes = thumbnailService.GetMissingThumbnailSizes(asset.Id);
                        
                        if (missingSizes.Any())
                        {
                            // Regenerate only missing thumbnails
                            var existingThumbnails = asset.Thumbnails.ToList();
                            var existingSizes = existingThumbnails.Select(t => t.Size).ToHashSet();
                            
                            // Remove database entries for thumbnails that don't exist physically
                            var thumbnailsToRemove = existingThumbnails
                                .Where(t => missingSizes.Contains(t.Size))
                                .ToList();
                            
                            if (thumbnailsToRemove.Any())
                            {
                                dbContext.AssetThumbnails.RemoveRange(thumbnailsToRemove);
                            }
                            
                            // Generate missing thumbnails
                            var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(
                                asset.FullPath,
                                asset.Id,
                                cancellationToken);
                            
                            // Only add the ones that were missing
                            var newThumbnails = allThumbnails
                                .Where(t => missingSizes.Contains(t.Size))
                                .ToList();
                            
                            if (newThumbnails.Any())
                            {
                                dbContext.AssetThumbnails.AddRange(newThumbnails);
                                stats.ThumbnailsRegenerated += newThumbnails.Count;
                            }
                        }
                    }
                    
                    // Save regenerated thumbnails
                    if (assetsToUpdate.Any(a => a.Type == AssetType.IMAGE))
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                
                // STEP 4c: Verify and regenerate thumbnails for ALL scanned assets (including unchanged ones)
                var unchangedImageAssets = allScannedAssets
                    .Where(a => a.Type == AssetType.IMAGE && !assetsToUpdate.Contains(a))
                    .ToList();
                
                if (unchangedImageAssets.Any())
                {
                    foreach (var asset in unchangedImageAssets)
                    {
                        // Load thumbnails from database
                        await dbContext.Entry(asset)
                            .Collection(a => a.Thumbnails)
                            .LoadAsync(cancellationToken);
                        
                        // Check which thumbnails are missing physically
                        var missingSizes = thumbnailService.GetMissingThumbnailSizes(asset.Id);
                        
                        if (missingSizes.Any())
                        {
                            // Remove database entries for thumbnails that don't exist physically
                            var thumbnailsToRemove = asset.Thumbnails
                                .Where(t => missingSizes.Contains(t.Size))
                                .ToList();
                            
                            if (thumbnailsToRemove.Any())
                            {
                                dbContext.AssetThumbnails.RemoveRange(thumbnailsToRemove);
                            }
                            
                            // Generate missing thumbnails
                            var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(
                                asset.FullPath,
                                asset.Id,
                                cancellationToken);
                            
                            // Only add the ones that were missing
                            var newThumbnails = allThumbnails
                                .Where(t => missingSizes.Contains(t.Size))
                                .ToList();
                            
                            if (newThumbnails.Any())
                            {
                                dbContext.AssetThumbnails.AddRange(newThumbnails);
                                stats.ThumbnailsRegenerated += newThumbnails.Count;
                            }
                        }
                    }
                    
                    // Save regenerated thumbnails for unchanged assets
                    if (unchangedImageAssets.Any())
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                
                // STEP 6: Cleanup - Remove orphaned assets (files that no longer exist)
                var allAssetPaths = await dbContext.Assets
                    .Select(a => a.FullPath)
                    .ToListAsync(cancellationToken);
                
                var orphanedPaths = allAssetPaths.Except(assetsToDelete).ToList();
                if (orphanedPaths.Any())
                {
                    var orphanedAssets = await dbContext.Assets
                        .Where(a => orphanedPaths.Contains(a.FullPath))
                        .ToListAsync(cancellationToken);
                    
                    // Delete thumbnails and EXIF first (cascade should handle this, but explicit for safety)
                    var orphanedAssetIds = orphanedAssets.Select(a => a.Id).ToList();
                    var orphanedThumbnails = await dbContext.AssetThumbnails
                        .Where(t => orphanedAssetIds.Contains(t.AssetId))
                        .ToListAsync(cancellationToken);
                    
                    var orphanedExifs = await dbContext.AssetExifs
                        .Where(e => orphanedAssetIds.Contains(e.AssetId))
                        .ToListAsync(cancellationToken);
                    
                    dbContext.AssetThumbnails.RemoveRange(orphanedThumbnails);
                    dbContext.AssetExifs.RemoveRange(orphanedExifs);
                    dbContext.Assets.RemoveRange(orphanedAssets);
                    
                    stats.OrphanedFilesRemoved = orphanedAssets.Count;
                }
                
                // STEP 6b: Cleanup - Remove orphaned folders (directories that no longer exist or have no assets)
                // Normalize processed directories for comparison
                var normalizedProcessedDirs = processedDirectories
                    .Select(d => d.Replace('\\', '/').TrimEnd('/'))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToHashSet();
                
                // Get all folder IDs that have assets
                var foldersWithAssets = await dbContext.Assets
                    .Where(a => a.FolderId != null)
                    .Select(a => a.FolderId!.Value)
                    .Distinct()
                    .ToHashSetAsync(cancellationToken);
                
                // Get all folders that exist in the filesystem (from processedDirectories)
                var allFolders = await dbContext.Folders
                    .Include(f => f.Assets)
                    .Include(f => f.Permissions)
                    .Include(f => f.SubFolders)
                    .ToListAsync(cancellationToken);
                
                // Build a set of folder IDs that should be kept (have assets or are ancestors of folders with assets)
                var foldersToKeep = new HashSet<int>();
                
                // Add folders that have assets directly
                foldersToKeep.UnionWith(foldersWithAssets);
                
                // Recursively add parent folders of folders with assets
                void AddParentFolders(int folderId)
                {
                    var folder = allFolders.FirstOrDefault(f => f.Id == folderId);
                    if (folder?.ParentFolderId != null && !foldersToKeep.Contains(folder.ParentFolderId.Value))
                    {
                        foldersToKeep.Add(folder.ParentFolderId.Value);
                        AddParentFolders(folder.ParentFolderId.Value);
                    }
                }
                
                foreach (var folderId in foldersWithAssets)
                {
                    AddParentFolders(folderId);
                }
                
                var orphanedFolders = new List<Folder>();
                
                foreach (var folder in allFolders)
                {
                    var normalizedPath = folder.Path.Replace('\\', '/').TrimEnd('/');
                    
                    // Check if folder should be kept:
                    // 1. Has assets associated
                    // 2. Has permissions (don't delete folders with permissions)
                    // 3. Is an ancestor of a folder that has assets (in foldersToKeep)
                    // 4. Exists in the filesystem (normalizedProcessedDirs)
                    
                    bool hasAssets = folder.Assets.Any();
                    bool hasPermissions = folder.Permissions.Any();
                    bool existsInFilesystem = normalizedProcessedDirs.Contains(normalizedPath);
                    bool isAncestorOfFolderWithAssets = foldersToKeep.Contains(folder.Id);
                    
                    // Only delete if folder has no assets, no permissions, doesn't exist in filesystem, 
                    // and is not an ancestor of a folder with assets
                    if (!hasAssets && !hasPermissions && !existsInFilesystem && !isAncestorOfFolderWithAssets)
                    {
                        orphanedFolders.Add(folder);
                    }
                }
                
                if (orphanedFolders.Any())
                {
                    // Delete folders from bottom to top (children first) to avoid foreign key issues
                    var foldersToDelete = orphanedFolders
                        .OrderByDescending(f => f.Path.Count(c => c == '/' || c == '\\'))
                        .ToList();
                    
                    dbContext.Folders.RemoveRange(foldersToDelete);
                    stats.OrphanedFoldersRemoved = foldersToDelete.Count;
                }
                
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                
                // STEP 7: Finalization
                stats.ScanCompletedAt = DateTime.UtcNow;
                stats.ScanDuration = stats.ScanCompletedAt - scanStartTime;
                
                var response = new ScanAssetsResponse
                {
                    Statistics = stats,
                    AssetsProcessed = assetsToCreate.Count + assetsToUpdate.Count,
                    Message = $"Scan completed successfully. Processed {stats.NewFiles} new, {stats.UpdatedFiles} updated, {stats.OrphanedFilesRemoved} files and {stats.OrphanedFoldersRemoved} folders removed. Generated {stats.ThumbnailsGenerated} new thumbnails, regenerated {stats.ThumbnailsRegenerated} missing thumbnails."
                };
                
                return Results.Ok(response);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
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
    
    private async Task<Folder?> GetOrCreateFolderForPathAsync(
        ApplicationDbContext dbContext,
        string? folderPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(folderPath))
            return null;
        
        var normalizedPath = folderPath.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(normalizedPath))
            return null;
        
        var folder = await dbContext.Folders
            .FirstOrDefaultAsync(f => f.Path == normalizedPath, cancellationToken);
        
        if (folder != null)
            return folder;
        
        // Ensure parent exists
        var parentPath = Path.GetDirectoryName(folderPath);
        Folder? parentFolder = null;
        
        if (!string.IsNullOrEmpty(parentPath) && parentPath != folderPath)
        {
            parentFolder = await GetOrCreateFolderForPathAsync(dbContext, parentPath, cancellationToken);
        }
        
        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = normalizedPath.Split('/').LastOrDefault() ?? normalizedPath;
        }
        
        folder = new Folder
        {
            Path = normalizedPath,
            Name = folderName,
            ParentFolderId = parentFolder?.Id,
            CreatedAt = DateTime.UtcNow
        };
        
        dbContext.Folders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        return folder;
    }
    
    private async Task EnsureFolderStructureExistsAsync(
        ApplicationDbContext dbContext,
        string folderPath,
        CancellationToken cancellationToken)
    {
        await GetOrCreateFolderForPathAsync(dbContext, folderPath, cancellationToken);
    }
}

public class ScanStatistics
{
    public int TotalFilesFound { get; set; }
    public int NewFiles { get; set; }
    public int UpdatedFiles { get; set; }
    public int MovedFiles { get; set; }
    public int SkippedUnchanged { get; set; }
    public int OrphanedFilesRemoved { get; set; }
    public int OrphanedFoldersRemoved { get; set; }
    public int HashesCalculated { get; set; }
    public int ExifExtracted { get; set; }
    public int ThumbnailsGenerated { get; set; }
    public int ThumbnailsRegenerated { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
}

