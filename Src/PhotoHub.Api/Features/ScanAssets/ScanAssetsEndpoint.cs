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
                    
                    // Now generate thumbnails for new assets (need asset.Id)
                    foreach (var asset in assetsToCreate.Where(a => a.Type == AssetType.IMAGE))
                    {
                        var thumbnails = await thumbnailService.GenerateThumbnailsAsync(
                            asset.FullPath, 
                            asset.Id, 
                            cancellationToken);
                        
                        if (thumbnails.Any())
                        {
                            asset.Thumbnails = thumbnails;
                            stats.ThumbnailsGenerated += thumbnails.Count;
                        }
                    }
                }
                
                // Update existing assets
                if (assetsToUpdate.Any())
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
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
                
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                
                // STEP 7: Finalization
                stats.ScanCompletedAt = DateTime.UtcNow;
                stats.ScanDuration = stats.ScanCompletedAt - scanStartTime;
                
                var response = new ScanAssetsResponse
                {
                    Statistics = stats,
                    AssetsProcessed = assetsToCreate.Count + assetsToUpdate.Count,
                    Message = $"Scan completed successfully. Processed {stats.NewFiles} new, {stats.UpdatedFiles} updated, {stats.OrphanedFilesRemoved} removed."
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
    public int HashesCalculated { get; set; }
    public int ExifExtracted { get; set; }
    public int ThumbnailsGenerated { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
}

