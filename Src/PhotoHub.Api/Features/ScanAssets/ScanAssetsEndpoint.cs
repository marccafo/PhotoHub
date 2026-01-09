using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using Scalar.AspNetCore;

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
        [FromServices] MediaRecognitionService mediaRecognitionService,
        [FromServices] IMlJobService mlJobService,
        [FromServices] ApplicationDbContext dbContext,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var scanStartTime = DateTime.UtcNow;
        var stats = new ScanStatistics();
        
        try
        {
            // STEP 1: Recursive file discovery
            var scannedFiles = await DiscoverFilesAsync(directoryScanner, directoryPath, stats, cancellationToken);
            
            // Load existing assets for differential comparison
            var existingAssetsByChecksum = await dbContext.Assets
                .ToDictionaryAsync(a => a.Checksum, a => a, cancellationToken);
            
            var existingAssetsByPath = await dbContext.Assets
                .ToDictionaryAsync(a => a.FullPath, a => a, cancellationToken);
            
            // STEP 2 & 3: Process files (change detection, EXIF extraction, recognition)
            var scanContext = await ProcessFilesAsync(
                scannedFiles,
                existingAssetsByChecksum,
                existingAssetsByPath,
                dbContext,
                hashService,
                exifService,
                mediaRecognitionService,
                scanStartTime,
                stats,
                cancellationToken);
            
            // STEP 4: Database operations and thumbnail generation (atomic transaction)
            await ProcessDatabaseOperationsAsync(
                scanContext,
                dbContext,
                thumbnailService,
                mediaRecognitionService,
                mlJobService,
                stats,
                cancellationToken);
            
            // STEP 6: Finalization
            stats.ScanCompletedAt = DateTime.UtcNow;
            stats.ScanDuration = stats.ScanCompletedAt - scanStartTime;
            
            var response = new ScanAssetsResponse
            {
                Statistics = stats,
                AssetsProcessed = scanContext.AssetsToCreate.Count + scanContext.AssetsToUpdate.Count,
                Message = $"Scan completed successfully. Processed {stats.NewFiles} new, {stats.UpdatedFiles} updated, {stats.OrphanedFilesRemoved} files and {stats.OrphanedFoldersRemoved} folders removed. Generated {stats.ThumbnailsGenerated} new thumbnails, regenerated {stats.ThumbnailsRegenerated} missing thumbnails."
            };
            
            return Results.Ok(response);
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

    // STEP 1: Recursive file discovery
    private async Task<IEnumerable<ScannedFile>> DiscoverFilesAsync(
        DirectoryScanner directoryScanner,
        string directoryPath,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var scannedFiles = await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken);
        stats.TotalFilesFound = scannedFiles.Count();
        return scannedFiles;
    }

    // STEP 2 & 3: Process files (change detection, EXIF extraction, recognition)
    private async Task<ScanContext> ProcessFilesAsync(
        IEnumerable<ScannedFile> scannedFiles,
        Dictionary<string, Asset> existingAssetsByChecksum,
        Dictionary<string, Asset> existingAssetsByPath,
        ApplicationDbContext dbContext,
        FileHashService hashService,
        ExifExtractorService exifService,
        MediaRecognitionService mediaRecognitionService,
        DateTime scanStartTime,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var context = new ScanContext
        {
            AssetsToCreate = new List<Asset>(),
            AssetsToUpdate = new List<Asset>(),
            AssetsToDelete = new HashSet<string>(),
            AllScannedAssets = new HashSet<Asset>(),
            ProcessedDirectories = new HashSet<string>()
        };

        foreach (var file in scannedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            context.AssetsToDelete.Add(file.FullPath);
            
            // Ensure folder structure exists
            await EnsureFolderStructureForFileAsync(dbContext, file.FullPath, context.ProcessedDirectories, cancellationToken);
            
            // STEP 2: Change verification (differential)
            var changeResult = await VerifyFileChangesAsync(
                file,
                existingAssetsByPath,
                existingAssetsByChecksum,
                hashService,
                dbContext,
                stats,
                cancellationToken);
            
            if (changeResult.ShouldSkip)
            {
                if (changeResult.ExistingAsset != null)
                {
                    context.AllScannedAssets.Add(changeResult.ExistingAsset);
                }
                continue;
            }
            
            var asset = changeResult.Asset!;
            var isNew = changeResult.IsNew;
            
            // STEP 3: Extract EXIF metadata (only for new images)
            if (ShouldExtractExif(asset, isNew))
            {
                await ExtractExifMetadataAsync(
                    asset,
                    file.FullPath,
                    exifService,
                    stats,
                    cancellationToken);
            }
            
            // STEP 3b: Basic recognition - Detect media type tags (only if EXIF exists)
            if (asset.Exif != null)
            {
                await DetectMediaTagsAsync(
                    asset,
                    file.FullPath,
                    mediaRecognitionService,
                    stats,
                    cancellationToken);
            }
            
            if (!isNew)
            {
                context.AssetsToUpdate.Add(asset);
                context.AllScannedAssets.Add(asset);
            }
            else
            {
                asset.ScannedAt = scanStartTime;
            }
        }
        
        return context;
    }

    private async Task EnsureFolderStructureForFileAsync(
        ApplicationDbContext dbContext,
        string filePath,
        HashSet<string> processedDirectories,
        CancellationToken cancellationToken)
    {
        var fileDirectory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileDirectory) && !processedDirectories.Contains(fileDirectory))
        {
            await EnsureFolderStructureExistsAsync(dbContext, fileDirectory, cancellationToken);
            processedDirectories.Add(fileDirectory);
        }
    }

    private async Task<FileChangeResult> VerifyFileChangesAsync(
        ScannedFile file,
        Dictionary<string, Asset> existingAssetsByPath,
        Dictionary<string, Asset> existingAssetsByChecksum,
        FileHashService hashService,
        ApplicationDbContext dbContext,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var existingByPath = existingAssetsByPath.GetValueOrDefault(file.FullPath);
        var needsFullCheck = existingByPath == null || 
            hashService.HasFileChanged(file.FullPath, existingByPath.FileSize, existingByPath.ModifiedDate);
        
        if (!needsFullCheck)
        {
            stats.SkippedUnchanged++;
            return new FileChangeResult { ShouldSkip = true, ExistingAsset = existingByPath };
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
            var fileDirectory = Path.GetDirectoryName(file.FullPath);
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
                FolderId = folder?.Id
            };
            
            isNew = true;
            stats.NewFiles++;
        }
        
        return new FileChangeResult { Asset = asset, IsNew = isNew };
    }

    // Método separado para decidir si debe extraerse EXIF
    private static bool ShouldExtractExif(Asset asset, bool isNew)
    {
        return asset.Type == AssetType.IMAGE && isNew;
    }

    // Método que SOLO extrae EXIF (sin lógica de decisión)
    private async Task ExtractExifMetadataAsync(
        Asset asset,
        string filePath,
        ExifExtractorService exifService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var extractedExif = await exifService.ExtractExifAsync(filePath, cancellationToken);
        if (extractedExif != null)
        {
            asset.Exif = extractedExif;
            stats.ExifExtracted++;
        }
    }

    // Método que SOLO detecta tags (sin lógica de decisión)
    private async Task DetectMediaTagsAsync(
        Asset asset,
        string filePath,
        MediaRecognitionService mediaRecognitionService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        if (asset.Exif == null)
            return;
        
        var detectedTags = await mediaRecognitionService.DetectMediaTypeAsync(
            filePath,
            asset.Exif,
            cancellationToken);
        
        if (detectedTags.Any())
        {
            asset.Tags = detectedTags.Select(t => new AssetTag
            {
                TagType = t,
                DetectedAt = DateTime.UtcNow
            }).ToList();
            stats.MediaTagsDetected += detectedTags.Count;
        }
    }

    // STEP 4: Database operations and thumbnail generation (atomic transaction)
    private async Task ProcessDatabaseOperationsAsync(
        ScanContext context,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // STEP 4a: Insert new assets
            await InsertNewAssetsAsync(
                context.AssetsToCreate,
                dbContext,
                thumbnailService,
                mediaRecognitionService,
                mlJobService,
                stats,
                cancellationToken);
            
            // STEP 4d: Update existing assets
            await UpdateExistingAssetsAsync(
                context.AssetsToUpdate,
                dbContext,
                thumbnailService,
                stats,
                cancellationToken);
            
            // STEP 4f: Verify and regenerate thumbnails for ALL scanned assets
            await VerifyThumbnailsForAllAssetsAsync(
                context.AllScannedAssets,
                context.AssetsToUpdate,
                dbContext,
                thumbnailService,
                stats,
                cancellationToken);
            
            // STEP 5: Cleanup - Remove orphaned assets
            await RemoveOrphanedAssetsAsync(
                context.AssetsToDelete,
                dbContext,
                stats,
                cancellationToken);
            
            // STEP 5b: Cleanup - Remove orphaned folders
            await RemoveOrphanedFoldersAsync(
                context.ProcessedDirectories,
                dbContext,
                stats,
                cancellationToken);
            
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task InsertNewAssetsAsync(
        List<Asset> assetsToCreate,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        if (!assetsToCreate.Any())
            return;
        
        // Set folder IDs for new assets (in case they weren't set during creation)
        foreach (var asset in assetsToCreate)
        {
            if (asset.FolderId == null && !string.IsNullOrEmpty(asset.FullPath))
            {
                var fileDirectory = Path.GetDirectoryName(asset.FullPath);
                var folder = await GetOrCreateFolderForPathAsync(dbContext, fileDirectory, cancellationToken);
                asset.FolderId = folder?.Id;
            }
        }
        
        dbContext.Assets.AddRange(assetsToCreate);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        // STEP 4b: Generate thumbnails for new assets
        await GenerateThumbnailsForNewAssetsAsync(
            assetsToCreate,
            dbContext,
            thumbnailService,
            stats,
            cancellationToken);
        
        // STEP 4c: Queue ML jobs for new assets
        await QueueMlJobsForNewAssetsAsync(
            assetsToCreate,
            dbContext,
            mediaRecognitionService,
            mlJobService,
            stats,
            cancellationToken);
    }

    private async Task GenerateThumbnailsForNewAssetsAsync(
        List<Asset> assets,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var imageAssets = assets.Where(a => a.Type == AssetType.IMAGE).ToList();
        if (!imageAssets.Any())
            return;
        
        foreach (var asset in imageAssets)
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
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task QueueMlJobsForNewAssetsAsync(
        List<Asset> assets,
        ApplicationDbContext dbContext,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var imageAssets = assets.Where(a => a.Type == AssetType.IMAGE).ToList();
        if (!imageAssets.Any())
            return;
        
        foreach (var asset in imageAssets)
        {
            await dbContext.Entry(asset)
                .Reference(a => a.Exif)
                .LoadAsync(cancellationToken);
            
            if (mediaRecognitionService.ShouldTriggerMlJob(asset, asset.Exif))
            {
                await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.FaceDetection, cancellationToken);
                await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.ObjectRecognition, cancellationToken);
                stats.MlJobsQueued += 2;
            }
        }
    }

    private async Task UpdateExistingAssetsAsync(
        List<Asset> assetsToUpdate,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        if (!assetsToUpdate.Any())
            return;
        
        await dbContext.SaveChangesAsync(cancellationToken);
        
        // STEP 4e: Regenerate missing thumbnails for existing assets
        await RegenerateMissingThumbnailsAsync(
            assetsToUpdate,
            dbContext,
            thumbnailService,
            stats,
            cancellationToken);
    }

    private async Task RegenerateMissingThumbnailsAsync(
        List<Asset> assets,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var imageAssets = assets.Where(a => a.Type == AssetType.IMAGE).ToList();
        if (!imageAssets.Any())
            return;
        
        foreach (var asset in imageAssets)
        {
            await dbContext.Entry(asset)
                .Collection(a => a.Thumbnails)
                .LoadAsync(cancellationToken);
            
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
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task VerifyThumbnailsForAllAssetsAsync(
        HashSet<Asset> allScannedAssets,
        List<Asset> assetsToUpdate,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var unchangedImageAssets = allScannedAssets
            .Where(a => a.Type == AssetType.IMAGE && !assetsToUpdate.Contains(a))
            .ToList();
        
        if (!unchangedImageAssets.Any())
            return;
        
        foreach (var asset in unchangedImageAssets)
        {
            await dbContext.Entry(asset)
                .Collection(a => a.Thumbnails)
                .LoadAsync(cancellationToken);
            
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
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveOrphanedAssetsAsync(
        HashSet<string> assetsToDelete,
        ApplicationDbContext dbContext,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
        var allAssetPaths = await dbContext.Assets
            .Select(a => a.FullPath)
            .ToListAsync(cancellationToken);
        
        var orphanedPaths = allAssetPaths.Except(assetsToDelete).ToList();
        if (!orphanedPaths.Any())
            return;
        
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

    private async Task RemoveOrphanedFoldersAsync(
        HashSet<string> processedDirectories,
        ApplicationDbContext dbContext,
        ScanStatistics stats,
        CancellationToken cancellationToken)
    {
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

// Helper classes for refactoring
internal class ScanContext
{
    public List<Asset> AssetsToCreate { get; set; } = new();
    public List<Asset> AssetsToUpdate { get; set; } = new();
    public HashSet<string> AssetsToDelete { get; set; } = new();
    public HashSet<Asset> AllScannedAssets { get; set; } = new();
    public HashSet<string> ProcessedDirectories { get; set; } = new();
}

internal class FileChangeResult
{
    public Asset? Asset { get; set; }
    public bool IsNew { get; set; }
    public bool ShouldSkip { get; set; }
    public Asset? ExistingAsset { get; set; }
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
    public int MediaTagsDetected { get; set; }
    public int MlJobsQueued { get; set; }
    public int ThumbnailsGenerated { get; set; }
    public int ThumbnailsRegenerated { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
}
