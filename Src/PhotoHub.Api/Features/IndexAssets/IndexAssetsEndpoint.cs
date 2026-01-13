using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;
using PhotoHub.Blazor.Shared.Models;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.IndexAssets;

public class IndexAssetsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var serviceProvider = app.ServiceProvider;
        app.MapGet("/api/assets/index/stream", (
            [FromServices] DirectoryScanner directoryScanner,
            [FromServices] FileHashService hashService,
            [FromServices] ExifExtractorService exifService,
            [FromServices] ThumbnailGeneratorService thumbnailService,
            [FromServices] MediaRecognitionService mediaRecognitionService,
            [FromServices] IMlJobService mlJobService,
            [FromServices] SettingsService settingsService,
            [FromServices] ApplicationDbContext dbContext,
            CancellationToken cancellationToken) => HandleStream(directoryScanner, hashService, exifService, thumbnailService, mediaRecognitionService, mlJobService, settingsService, dbContext, serviceProvider, cancellationToken))
        .WithName("IndexAssetsStream")
        .WithTags("Assets")
        .WithDescription("Streams the scanning process progress for the internal assets directory");

        app.MapGet("/api/assets/index", Handle)
        .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/assets/scan\" -H \"Accept: application/json\"",
                label: "cURL Example")
        .WithName("IndexAssets")
        .WithTags("Assets")
        .WithDescription("Scans the internal assets directory, extracts metadata, generates thumbnails, and updates the database with all found media files. Supports images (JPG, PNG, etc.) and videos (MP4, AVI, etc.)")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Scans the internal assets directory";
            operation.Description = "This endpoint recursively scans the internal assets directory (ASSETS_PATH), extracts metadata, generates thumbnails, and updates the database with all found media files. Supports images (JPG, PNG, etc.) and videos (MP4, AVI, etc.)";
            return Task.CompletedTask;
        });
    }

    private async IAsyncEnumerable<IndexProgressUpdate> HandleStream(
        DirectoryScanner directoryScanner,
        FileHashService hashService,
        ExifExtractorService exifService,
        ThumbnailGeneratorService thumbnailService,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        SettingsService settingsService,
        ApplicationDbContext dbContext,
        IServiceProvider serviceProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<IndexProgressUpdate>();
        var indexStartTime = DateTime.UtcNow;
        var stats = new IndexStatistics();

        // Background task to perform the scan
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var scopedSettingsService = scope.ServiceProvider.GetRequiredService<SettingsService>();
                var scopedDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var scopedHashService = scope.ServiceProvider.GetRequiredService<FileHashService>();
                var scopedExifService = scope.ServiceProvider.GetRequiredService<ExifExtractorService>();
                var scopedMediaRecognitionService = scope.ServiceProvider.GetRequiredService<MediaRecognitionService>();
                var scopedThumbnailService = scope.ServiceProvider.GetRequiredService<ThumbnailGeneratorService>();
                var scopedMlJobService = scope.ServiceProvider.GetRequiredService<IMlJobService>();
                
                // Obtener la ruta interna del NAS (ASSETS_PATH)
                var directoryPath = scopedSettingsService.GetInternalAssetsPath();
                Console.WriteLine($"[SCAN] Indexando directorio interno: {directoryPath}");
                
                if (!Directory.Exists(directoryPath))
                {
                    await channel.Writer.WriteAsync(new IndexProgressUpdate 
                    { 
                        Message = $"Error: El directorio interno no existe: {directoryPath}", 
                        IsCompleted = true 
                    }, cancellationToken);
                    return;
                }
                
                await channel.Writer.WriteAsync(new IndexProgressUpdate { Message = "Iniciando descubrimiento de archivos...", Percentage = 0 }, cancellationToken);
                
                // STEP 1: Recursive file discovery
                var scannedFilesList = (await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken)).ToList();
                stats.TotalFilesFound = scannedFilesList.Count;
                
                await channel.Writer.WriteAsync(new IndexProgressUpdate { Message = $"Descubiertos {stats.TotalFilesFound} archivos. Procesando...", Percentage = 10, Statistics = MapToBlazorStats(stats) }, cancellationToken);

                // Load existing assets for differential comparison
                // Manejar duplicados: si hay múltiples assets con el mismo checksum, tomar el más reciente
                var allAssets = await dbContext.Assets.ToListAsync(cancellationToken);
                var existingAssetsByChecksum = allAssets
                    .Where(a => !string.IsNullOrEmpty(a.Checksum))
                    .GroupBy(a => a.Checksum)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ScannedAt).First());
                
                var existingAssetsByPath = allAssets
                    .Where(a => !string.IsNullOrEmpty(a.FullPath))
                    .GroupBy(a => a.FullPath)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ScannedAt).First());
                
                // STEP 2 & 3: Process files
                var indexContext = new IndexContext
                {
                    AssetsToCreate = new List<Asset>(),
                    AssetsToUpdate = new List<Asset>(),
                    AssetsToDelete = new HashSet<string>(), // Ahora guardará las rutas virtualizadas de los archivos ENCONTRADOS
                    AllScannedAssets = new HashSet<Asset>(),
                    ProcessedDirectories = new HashSet<string>()
                };

                int processedCount = 0;
                
                foreach (var file in scannedFilesList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Normalizar la ruta del archivo para comparar con la BD
                    var dbPath = await scopedSettingsService.VirtualizePathAsync(file.FullPath);
                    indexContext.AssetsToDelete.Add(dbPath); // Añadir a la lista de archivos que SI existen
                    
                    var fileDirectory = Path.GetDirectoryName(file.FullPath);
                    if (!string.IsNullOrEmpty(fileDirectory))
                    {
                        var virtualFolder = await scopedSettingsService.VirtualizePathAsync(fileDirectory);
                        indexContext.ProcessedDirectories.Add(virtualFolder);
                    }
                    
                    await EnsureFolderStructureForFileAsync(scopedDbContext, file.FullPath, new HashSet<string>(), cancellationToken);
                    
                    var changeResult = await VerifyFileChangesAsync(file, existingAssetsByPath, existingAssetsByChecksum, scopedHashService, scopedDbContext, stats, scopedSettingsService, cancellationToken);
                    
                    if (!changeResult.ShouldSkip)
                    {
                        var asset = changeResult.Asset!;
                        var isNew = changeResult.IsNew;
                        if (ShouldExtractExif(asset, isNew)) 
                            await ExtractExifMetadataAsync(asset, file.FullPath, exifService, stats, cancellationToken);
                        
                        if (asset.Exif != null)
                            await DetectMediaTagsAsync(asset, file.FullPath, mediaRecognitionService, stats, cancellationToken);
                        
                        if (!isNew)
                        {
                            indexContext.AssetsToUpdate.Add(asset);
                            indexContext.AllScannedAssets.Add(asset);
                        }
                        else
                        {
                            asset.ScannedAt = indexStartTime;
                            indexContext.AssetsToCreate.Add(asset);
                        }
                    }
                    else if (changeResult.ExistingAsset != null)
                    {
                        indexContext.AllScannedAssets.Add(changeResult.ExistingAsset);
                    }

                    processedCount++;
                    if (processedCount % 10 == 0 || processedCount == scannedFilesList.Count)
                    {
                        await channel.Writer.WriteAsync(new IndexProgressUpdate 
                        { 
                            Message = $"Procesando archivo {processedCount} de {stats.TotalFilesFound}...", 
                            Percentage = 10 + (processedCount * 40.0 / scannedFilesList.Count),
                            Statistics = MapToBlazorStats(stats)
                        }, cancellationToken);
                    }
                }

                // STEP 4: Database operations and thumbnail generation
                await channel.Writer.WriteAsync(new IndexProgressUpdate { Message = "Guardando cambios y generando miniaturas...", Percentage = 50, Statistics = MapToBlazorStats(stats) }, cancellationToken);
                
                await ProcessDatabaseOperationsWithProgressAsync(indexContext, dbContext, thumbnailService, mediaRecognitionService, mlJobService, settingsService, channel.Writer, stats, cancellationToken);

                stats.IndexCompletedAt = DateTime.UtcNow;
                stats.IndexDuration = stats.IndexCompletedAt - indexStartTime;
                
                await channel.Writer.WriteAsync(new IndexProgressUpdate 
                { 
                    Message = "Indexación completada con éxito.", 
                    Percentage = 100, 
                    Statistics = MapToBlazorStats(stats),
                    IsCompleted = true
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                await channel.Writer.WriteAsync(new IndexProgressUpdate { Message = $"Error: {ex.Message}", IsCompleted = true }, cancellationToken);
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Stream the updates from the channel
        await foreach (var update in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }

    private PhotoHub.Blazor.Shared.Models.IndexStatistics MapToBlazorStats(IndexStatistics stats)
    {
        return new PhotoHub.Blazor.Shared.Models.IndexStatistics
        {
            TotalFilesFound = stats.TotalFilesFound,
            NewFiles = stats.NewFiles,
            UpdatedFiles = stats.UpdatedFiles,
            MovedFiles = stats.MovedFiles,
            SkippedUnchanged = stats.SkippedUnchanged,
            OrphanedFilesRemoved = stats.OrphanedFilesRemoved,
            OrphanedFoldersRemoved = stats.OrphanedFoldersRemoved,
            HashesCalculated = stats.HashesCalculated,
            ExifExtracted = stats.ExifExtracted,
            MediaTagsDetected = stats.MediaTagsDetected,
            MlJobsQueued = stats.MlJobsQueued,
            ThumbnailsGenerated = stats.ThumbnailsGenerated,
            ThumbnailsRegenerated = stats.ThumbnailsRegenerated,
            IndexCompletedAt = stats.IndexCompletedAt,
            IndexDuration = stats.IndexDuration
        };
    }

    private async Task ProcessDatabaseOperationsWithProgressAsync(
        IndexContext context,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        SettingsService settingsService,
        ChannelWriter<IndexProgressUpdate> writer,
        IndexStatistics apiStats,
        CancellationToken cancellationToken)
    {
        // STEP 4a: Cleanup - Remove orphaned assets
        await RemoveOrphanedAssetsAsync(context.AssetsToDelete, dbContext, apiStats, cancellationToken);
        await writer.WriteAsync(new IndexProgressUpdate { Message = "Limpieza de archivos huérfanos completada.", Percentage = 60, Statistics = MapToBlazorStats(apiStats) }, cancellationToken);

        // STEP 4b & 4c: Insert new assets and generate thumbnails
        if (context.AssetsToCreate.Any())
        {
            // Set folder IDs for new assets
            foreach (var asset in context.AssetsToCreate)
            {
                if (asset.FolderId == null && !string.IsNullOrEmpty(asset.FullPath))
                {
                    var fileDirectory = Path.GetDirectoryName(asset.FullPath);
                    var folder = await GetOrCreateFolderForPathAsync(dbContext, fileDirectory, cancellationToken);
                    asset.FolderId = folder?.Id;
                }
            }
            
            dbContext.Assets.AddRange(context.AssetsToCreate);
            await dbContext.SaveChangesAsync(cancellationToken);

            int count = 0;
            foreach (var asset in context.AssetsToCreate)
            {
                // Resolver la ruta física del archivo antes de generar miniaturas
                var physicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
                if (string.IsNullOrEmpty(physicalPath))
                {
                    Console.WriteLine($"[WARNING] Could not resolve physical path for asset {asset.Id}: {asset.FullPath}");
                    count++;
                    continue;
                }
                
                var thumbnails = await thumbnailService.GenerateThumbnailsAsync(physicalPath, asset.Id, cancellationToken);
                if (thumbnails.Any())
                {
                    dbContext.AssetThumbnails.AddRange(thumbnails);
                    apiStats.ThumbnailsGenerated += thumbnails.Count;
                    Console.WriteLine($"[THUMBNAIL] Generated {thumbnails.Count} thumbnails for asset {asset.Id} (Total: {apiStats.ThumbnailsGenerated})");
                }
                else
                {
                    Console.WriteLine($"[WARNING] No thumbnails generated for asset {asset.Id}: {physicalPath}");
                }
                
                if (mediaRecognitionService.ShouldTriggerMlJob(asset, asset.Exif))
                {
                    await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.FaceDetection, cancellationToken);
                    await mlJobService.EnqueueMlJobAsync(asset.Id, MlJobType.ObjectRecognition, cancellationToken);
                    apiStats.MlJobsQueued += 2;
                }

                count++;
                if (count % 5 == 0 || count == context.AssetsToCreate.Count)
                {
                    await writer.WriteAsync(new IndexProgressUpdate 
                    { 
                        Message = $"Generando miniaturas para nuevos archivos ({count}/{context.AssetsToCreate.Count})...", 
                        Percentage = 60 + (count * 15.0 / context.AssetsToCreate.Count),
                        Statistics = MapToBlazorStats(apiStats) 
                    }, cancellationToken);
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        
        await writer.WriteAsync(new IndexProgressUpdate { Message = "Nuevos archivos procesados.", Percentage = 75, Statistics = MapToBlazorStats(apiStats) }, cancellationToken);

        // STEP 4d & 4e: Update existing assets and regenerate thumbnails
        if (context.AssetsToUpdate.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            int count = 0;
            foreach (var asset in context.AssetsToUpdate)
            {
                var missingSizes = thumbnailService.GetMissingThumbnailSizes(asset.Id);
                if (missingSizes.Any())
                {
                    await dbContext.Entry(asset).Collection(a => a.Thumbnails).LoadAsync(cancellationToken);
                    var thumbnailsToRemove = asset.Thumbnails.Where(t => missingSizes.Contains(t.Size)).ToList();
                    if (thumbnailsToRemove.Any()) dbContext.AssetThumbnails.RemoveRange(thumbnailsToRemove);
                    
                    var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(asset.FullPath, asset.Id, cancellationToken);
                    var newThumbnails = allThumbnails.Where(t => missingSizes.Contains(t.Size)).ToList();
                    if (newThumbnails.Any())
                    {
                        dbContext.AssetThumbnails.AddRange(newThumbnails);
                        apiStats.ThumbnailsRegenerated += newThumbnails.Count;
                    }
                }
                
                count++;
                if (count % 5 == 0 || count == context.AssetsToUpdate.Count)
                {
                    await writer.WriteAsync(new IndexProgressUpdate 
                    { 
                        Message = $"Actualizando miniaturas ({count}/{context.AssetsToUpdate.Count})...", 
                        Percentage = 75 + (count * 10.0 / context.AssetsToUpdate.Count),
                        Statistics = MapToBlazorStats(apiStats) 
                    }, cancellationToken);
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await writer.WriteAsync(new IndexProgressUpdate { Message = "Archivos actualizados.", Percentage = 85, Statistics = MapToBlazorStats(apiStats) }, cancellationToken);

        // STEP 4f: Verify thumbnails for ALL scanned assets (those that didn't need update but might miss thumbnails)
        var unchangedAssets = context.AllScannedAssets
            .Where(a => !context.AssetsToUpdate.Contains(a) && !context.AssetsToCreate.Contains(a))
            .ToList();

        if (unchangedAssets.Any())
        {
            int count = 0;
            foreach (var asset in unchangedAssets)
            {
                var missingSizes = thumbnailService.GetMissingThumbnailSizes(asset.Id);
                if (missingSizes.Any())
                {
                    await dbContext.Entry(asset).Collection(a => a.Thumbnails).LoadAsync(cancellationToken);
                    var thumbnailsToRemove = asset.Thumbnails.Where(t => missingSizes.Contains(t.Size)).ToList();
                    if (thumbnailsToRemove.Any()) dbContext.AssetThumbnails.RemoveRange(thumbnailsToRemove);
                    
                    var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(asset.FullPath, asset.Id, cancellationToken);
                    var newThumbnails = allThumbnails.Where(t => missingSizes.Contains(t.Size)).ToList();
                    if (newThumbnails.Any())
                    {
                        dbContext.AssetThumbnails.AddRange(newThumbnails);
                        apiStats.ThumbnailsRegenerated += newThumbnails.Count;
                    }
                }

                count++;
                if (count % 10 == 0 || count == unchangedAssets.Count)
                {
                    await writer.WriteAsync(new IndexProgressUpdate 
                    { 
                        Message = $"Verificando miniaturas existentes ({count}/{unchangedAssets.Count})...", 
                        Percentage = 85 + (count * 10.0 / unchangedAssets.Count),
                        Statistics = MapToBlazorStats(apiStats) 
                    }, cancellationToken);
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await RemoveOrphanedFoldersAsync(context.ProcessedDirectories, dbContext, apiStats, cancellationToken);
        await writer.WriteAsync(new IndexProgressUpdate { Message = "Limpieza de carpetas completada.", Percentage = 98, Statistics = MapToBlazorStats(apiStats) }, cancellationToken);
    }


    private async Task<IResult> Handle(
        [FromServices] DirectoryScanner directoryScanner,
        [FromServices] FileHashService hashService,
        [FromServices] ExifExtractorService exifService,
        [FromServices] ThumbnailGeneratorService thumbnailService,
        [FromServices] MediaRecognitionService mediaRecognitionService,
        [FromServices] IMlJobService mlJobService,
        [FromServices] SettingsService settingsService,
        [FromServices] ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var indexStartTime = DateTime.UtcNow;
        var stats = new IndexStatistics();
        
        try
        {
            // Obtener la ruta interna del NAS (ASSETS_PATH)
            var directoryPath = settingsService.GetInternalAssetsPath();
            Console.WriteLine($"[SCAN] Indexando directorio interno: {directoryPath}");
            
            if (!Directory.Exists(directoryPath))
            {
                return Results.NotFound(new { error = $"El directorio interno no existe: {directoryPath}" });
            }
            
            // STEP 1: Recursive file discovery
            var scannedFiles = await DiscoverFilesAsync(directoryScanner, directoryPath, stats, cancellationToken);
            
            // Load existing assets for differential comparison
            // Manejar duplicados: si hay múltiples assets con el mismo checksum, tomar el más reciente
            var allAssets = await dbContext.Assets.ToListAsync(cancellationToken);
            var existingAssetsByChecksum = allAssets
                .Where(a => !string.IsNullOrEmpty(a.Checksum))
                .GroupBy(a => a.Checksum)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ScannedAt).First());
            
            var existingAssetsByPath = allAssets
                .Where(a => !string.IsNullOrEmpty(a.FullPath))
                .GroupBy(a => a.FullPath)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.ScannedAt).First());
            
            // STEP 2 & 3: Process files (change detection, EXIF extraction, recognition)
            var indexContext = await ProcessFilesAsync(
                scannedFiles,
                existingAssetsByChecksum,
                existingAssetsByPath,
                dbContext,
                hashService,
                exifService,
                mediaRecognitionService,
                settingsService,
                indexStartTime,
                stats,
                cancellationToken);
            
            // STEP 4: Database operations and thumbnail generation (atomic transaction)
            await ProcessDatabaseOperationsAsync(
                indexContext,
                dbContext,
                thumbnailService,
                mediaRecognitionService,
                mlJobService,
                settingsService,
                stats,
                cancellationToken);
            
            // STEP 6: Finalization
            stats.IndexCompletedAt = DateTime.UtcNow;
            stats.IndexDuration = stats.IndexCompletedAt - indexStartTime;
            
            var response = new IndexAssetsResponse
            {
                Statistics = MapToBlazorStats(stats),
                AssetsProcessed = indexContext.AssetsToCreate.Count + indexContext.AssetsToUpdate.Count,
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
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        var scannedFiles = await directoryScanner.ScanDirectoryAsync(directoryPath, cancellationToken);
        stats.TotalFilesFound = scannedFiles.Count();
        return scannedFiles;
    }

    // STEP 2 & 3: Process files (change detection, EXIF extraction, recognition)
    private async Task<IndexContext> ProcessFilesAsync(
        IEnumerable<ScannedFile> scannedFiles,
        Dictionary<string, Asset> existingAssetsByChecksum,
        Dictionary<string, Asset> existingAssetsByPath,
        ApplicationDbContext dbContext,
        FileHashService hashService,
        ExifExtractorService exifService,
        MediaRecognitionService mediaRecognitionService,
        SettingsService settingsService,
        DateTime indexStartTime,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        var context = new IndexContext
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
                settingsService,
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
                asset.ScannedAt = indexStartTime;
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
        IndexStatistics stats,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Normalizar la ruta del archivo indexado si está en la biblioteca gestionada para comparar con la BD
        var dbPath = await settingsService.VirtualizePathAsync(file.FullPath);

        var existingByPath = existingAssetsByPath.GetValueOrDefault(dbPath);
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
        
        if (existingByChecksum != null && existingByChecksum.FullPath != dbPath)
        {
            // File was moved/renamed - update path
            existingByChecksum.FullPath = dbPath;
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
                FullPath = dbPath,
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
        return (asset.Type == AssetType.IMAGE || asset.Type == AssetType.VIDEO) && isNew;
    }

    // Método que SOLO extrae EXIF (sin lógica de decisión)
    private async Task ExtractExifMetadataAsync(
        Asset asset,
        string filePath,
        ExifExtractorService exifService,
        IndexStatistics stats,
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
        IndexStatistics stats,
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
        IndexContext context,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        SettingsService settingsService,
        IndexStatistics stats,
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
                settingsService,
                stats,
                cancellationToken);
            
            // STEP 4d: Update existing assets
            await UpdateExistingAssetsAsync(
                context.AssetsToUpdate,
                dbContext,
                thumbnailService,
                settingsService,
                stats,
                cancellationToken);
            
            // STEP 4f: Verify and regenerate thumbnails for ALL scanned assets
            await VerifyThumbnailsForAllAssetsAsync(
                context.AllScannedAssets,
                context.AssetsToUpdate,
                dbContext,
                thumbnailService,
                settingsService,
                stats,
                cancellationToken);
            
            // STEP 5: Cleanup - Remove duplicate assets (same checksum)
            // Se ejecuta después de salvar cambios previos para que la BD refleje el estado actual
            await dbContext.SaveChangesAsync(cancellationToken);
            await RemoveDuplicateAssetsAsync(
                dbContext,
                stats,
                settingsService,
                cancellationToken);
            
            // STEP 5b: Cleanup - Remove orphaned assets
            await RemoveOrphanedAssetsAsync(
                context.AssetsToDelete,
                dbContext,
                stats,
                cancellationToken);
            
            // STEP 5c: Cleanup - Remove orphaned folders
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
        SettingsService settingsService,
        IndexStatistics stats,
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
            settingsService,
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
        SettingsService settingsService,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        if (!assets.Any())
            return;
        
        foreach (var asset in assets)
        {
            // Resolver la ruta física del archivo antes de generar miniaturas
            var physicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
            if (string.IsNullOrEmpty(physicalPath))
            {
                Console.WriteLine($"[WARNING] Could not resolve physical path for asset {asset.Id}: {asset.FullPath}");
                continue;
            }
            
            var thumbnails = await thumbnailService.GenerateThumbnailsAsync(
                physicalPath,
                asset.Id,
                cancellationToken);
            
            if (thumbnails.Any())
            {
                dbContext.AssetThumbnails.AddRange(thumbnails);
                stats.ThumbnailsGenerated += thumbnails.Count;
                Console.WriteLine($"[THUMBNAIL] Generated {thumbnails.Count} thumbnails for asset {asset.Id}");
            }
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task QueueMlJobsForNewAssetsAsync(
        List<Asset> assets,
        ApplicationDbContext dbContext,
        MediaRecognitionService mediaRecognitionService,
        IMlJobService mlJobService,
        IndexStatistics stats,
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
        SettingsService settingsService,
        IndexStatistics stats,
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
            settingsService,
            stats,
            cancellationToken);
    }

    private async Task RegenerateMissingThumbnailsAsync(
        List<Asset> assets,
        ApplicationDbContext dbContext,
        ThumbnailGeneratorService thumbnailService,
        SettingsService settingsService,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        if (!assets.Any())
            return;
        
        foreach (var asset in assets)
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
                
                // Resolver la ruta física del archivo antes de generar miniaturas
                var physicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
                if (!string.IsNullOrEmpty(physicalPath))
                {
                    // Generate missing thumbnails
                    var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(
                        physicalPath,
                        asset.Id,
                        cancellationToken);
                    
                    var newThumbnails = allThumbnails
                        .Where(t => missingSizes.Contains(t.Size))
                        .ToList();
                    
                    if (newThumbnails.Any())
                    {
                        dbContext.AssetThumbnails.AddRange(newThumbnails);
                        stats.ThumbnailsRegenerated += newThumbnails.Count;
                        Console.WriteLine($"[THUMBNAIL] Regenerated {newThumbnails.Count} thumbnails for updated asset {asset.Id}");
                    }
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
        SettingsService settingsService,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        var unchangedAssets = allScannedAssets
            .Where(a => !assetsToUpdate.Contains(a))
            .ToList();
        
        if (!unchangedAssets.Any())
            return;
        
        foreach (var asset in unchangedAssets)
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
                
                // Resolver la ruta física del archivo antes de generar miniaturas
                var physicalPath = await settingsService.ResolvePhysicalPathAsync(asset.FullPath);
                if (!string.IsNullOrEmpty(physicalPath))
                {
                    // Generate missing thumbnails
                    var allThumbnails = await thumbnailService.GenerateThumbnailsAsync(
                        physicalPath,
                        asset.Id,
                        cancellationToken);
                    
                    var newThumbnails = allThumbnails
                        .Where(t => missingSizes.Contains(t.Size))
                        .ToList();
                    
                    if (newThumbnails.Any())
                    {
                        dbContext.AssetThumbnails.AddRange(newThumbnails);
                        stats.ThumbnailsRegenerated += newThumbnails.Count;
                        Console.WriteLine($"[THUMBNAIL] Regenerated {newThumbnails.Count} thumbnails for unchanged asset {asset.Id}");
                    }
                }
            }
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveDuplicateAssetsAsync(
        ApplicationDbContext dbContext,
        IndexStatistics stats,
        SettingsService settingsService,
        CancellationToken cancellationToken)
    {
        // Encontrar assets duplicados por checksum
        // Usar AsNoTracking para evitar que EF mantenga demasiados objetos en memoria
        var allAssets = await dbContext.Assets
            .Where(a => !string.IsNullOrEmpty(a.Checksum))
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        var duplicateGroups = allAssets
            .GroupBy(a => a.Checksum)
            .Where(g => g.Count() > 1)
            .ToList();
        
        if (!duplicateGroups.Any())
            return;
        
        var duplicatesToRemoveIds = new List<int>();
        var assetsToUpdate = new List<Asset>();
        
        foreach (var group in duplicateGroups)
        {
            // Mantener el más reciente (por ScannedAt) o el que tiene ID más bajo si no tienen ScannedAt
            var assetsInGroup = group.OrderByDescending(a => a.ScannedAt).ThenBy(a => a.Id).ToList();
            var assetToKeep = assetsInGroup.First();
            var duplicates = assetsInGroup.Skip(1).ToList();
            
            // Verificar que el asset a mantener tenga el archivo físico
            var physicalPath = await settingsService.ResolvePhysicalPathAsync(assetToKeep.FullPath);
            if (!File.Exists(physicalPath))
            {
                // Si el asset a mantener no tiene archivo, intentar encontrar uno en el grupo que sí lo tenga
                Asset? assetWithFile = null;
                foreach (var a in assetsInGroup.Skip(1))
                {
                    var path = await settingsService.ResolvePhysicalPathAsync(a.FullPath);
                    if (File.Exists(path))
                    {
                        assetWithFile = a;
                        break;
                    }
                }
                
                if (assetWithFile != null)
                {
                    // El que íbamos a mantener no existe, pero este sí. Intercambiamos.
                    duplicatesToRemoveIds.Add(assetToKeep.Id);
                    assetToKeep = assetWithFile;
                    duplicates = assetsInGroup.Where(a => a.Id != assetToKeep.Id).ToList();
                }
                else
                {
                    // Ningún asset del grupo tiene archivo físico... esto es raro.
                    // Mantendremos el original y dejaremos que RemoveOrphanedAssets lo limpie si es necesario.
                }
            }
            
            duplicatesToRemoveIds.AddRange(duplicates.Select(d => d.Id));
            
            // Si hay múltiples assets con el mismo checksum pero diferentes rutas, 
            // actualizar el asset a mantener con la ruta más reciente
            var mostRecentPath = assetsInGroup
                .OrderByDescending(a => a.ScannedAt)
                .ThenByDescending(a => a.ModifiedDate)
                .First()
                .FullPath;
            
            if (assetToKeep.FullPath != mostRecentPath)
            {
                // Solo actualizar si realmente cambió
                var dbAssetToKeep = await dbContext.Assets.FindAsync(new object[] { assetToKeep.Id }, cancellationToken);
                if (dbAssetToKeep != null)
                {
                    dbAssetToKeep.FullPath = mostRecentPath;
                    dbAssetToKeep.FileName = Path.GetFileName(mostRecentPath);
                    assetsToUpdate.Add(dbAssetToKeep);
                }
            }
        }
        
        if (duplicatesToRemoveIds.Any())
        {
            // Eliminar duplicados de la base de datos de forma eficiente
            var duplicateThumbnails = await dbContext.AssetThumbnails
                .Where(t => duplicatesToRemoveIds.Contains(t.AssetId))
                .ToListAsync(cancellationToken);
            
            var duplicateExifs = await dbContext.AssetExifs
                .Where(e => duplicatesToRemoveIds.Contains(e.AssetId))
                .ToListAsync(cancellationToken);
            
            var assetsToRemove = await dbContext.Assets
                .Where(a => duplicatesToRemoveIds.Contains(a.Id))
                .ToListAsync(cancellationToken);
            
            dbContext.AssetThumbnails.RemoveRange(duplicateThumbnails);
            dbContext.AssetExifs.RemoveRange(duplicateExifs);
            dbContext.Assets.RemoveRange(assetsToRemove);
            
            await dbContext.SaveChangesAsync(cancellationToken);
            
            stats.DuplicateAssetsRemoved = duplicatesToRemoveIds.Count;
            Console.WriteLine($"[SCAN] Eliminados {duplicatesToRemoveIds.Count} assets duplicados");
        }
    }

    private async Task RemoveOrphanedAssetsAsync(
        HashSet<string> foundVirtualPaths,
        ApplicationDbContext dbContext,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        var allAssetPaths = await dbContext.Assets
            .Select(a => a.FullPath)
            .ToListAsync(cancellationToken);
        
        // Los huérfanos son los que están en la BD pero NO en el sistema de archivos (no encontrados en el scan)
        var orphanedPaths = allAssetPaths.Except(foundVirtualPaths).ToList();
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
        HashSet<string> foundVirtualDirectories,
        ApplicationDbContext dbContext,
        IndexStatistics stats,
        CancellationToken cancellationToken)
    {
        // Normalize found directories for comparison
        var normalizedFoundDirs = foundVirtualDirectories
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
            bool existsInFilesystem = normalizedFoundDirs.Contains(normalizedPath);
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
internal class IndexContext
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

public class IndexStatistics
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
    public int DuplicateAssetsRemoved { get; set; }
    public DateTime IndexCompletedAt { get; set; }
    public TimeSpan IndexDuration { get; set; }
}
