using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PhotoHub.Server.Api.Shared.Data;
using PhotoHub.Server.Api.Shared.Models;

namespace PhotoHub.Server.Api.Shared.Services;

public record ScanProgressUpdate(
    string Message,
    int Percentage,
    int AssetsFound,
    int AssetsIndexed,
    int AssetsMarkedOffline,
    bool IsCompleted,
    string? Error = null);

public class ExternalLibraryScanService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly DirectoryScanner _scanner;
    private readonly AssetIndexingService _indexingService;

    public ExternalLibraryScanService(
        ApplicationDbContext dbContext,
        DirectoryScanner scanner,
        AssetIndexingService indexingService)
    {
        _dbContext = dbContext;
        _scanner = scanner;
        _indexingService = indexingService;
    }

    public async IAsyncEnumerable<ScanProgressUpdate> ScanAsync(
        Guid libraryId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<ScanProgressUpdate>();

        var task = Task.Run(async () =>
        {
            try
            {
                await RunScanAsync(libraryId, channel.Writer, ct);
            }
            catch (Exception ex)
            {
                channel.Writer.TryWrite(new ScanProgressUpdate(
                    $"Unexpected error: {ex.Message}", 100, 0, 0, 0, true, ex.Message));
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, ct);

        await foreach (var update in channel.Reader.ReadAllAsync(ct))
            yield return update;

        await task;
    }

    private async Task RunScanAsync(
        Guid libraryId,
        ChannelWriter<ScanProgressUpdate> writer,
        CancellationToken ct)
    {
        var library = await _dbContext.ExternalLibraries
            .FirstOrDefaultAsync(l => l.Id == libraryId, ct);

        if (library == null)
        {
            await writer.WriteAsync(new ScanProgressUpdate(
                "Library not found.", 0, 0, 0, 0, true, "Library not found."), ct);
            return;
        }

        if (!Directory.Exists(library.Path))
        {
            await writer.WriteAsync(new ScanProgressUpdate(
                $"Directory not found: {library.Path}", 0, 0, 0, 0, true,
                $"Directory does not exist: {library.Path}"), ct);
            return;
        }

        // Mark as running
        library.LastScanStatus = ExternalLibraryScanStatus.Running;
        await _dbContext.SaveChangesAsync(ct);

        await writer.WriteAsync(new ScanProgressUpdate(
            "Scanning directory...", 5, 0, 0, 0, false), ct);

        // Discover files
        var scannedFiles = (await _scanner.ScanDirectoryAsync(library.Path, ct)).ToList();

        if (!library.ImportSubfolders)
        {
            var normalizedRoot = library.Path.TrimEnd(Path.DirectorySeparatorChar, '/');
            scannedFiles = scannedFiles
                .Where(f => string.Equals(
                    Path.GetDirectoryName(f.FullPath)?.TrimEnd(Path.DirectorySeparatorChar, '/'),
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var total = scannedFiles.Count;
        await writer.WriteAsync(new ScanProgressUpdate(
            $"Found {total} files. Indexing...", 10, total, 0, 0, false), ct);

        // Track existing paths to detect offline files after the scan
        var existingPaths = await _dbContext.Assets
            .Where(a => a.ExternalLibraryId == libraryId && a.DeletedAt == null)
            .Select(a => a.FullPath)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);

        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int indexed = 0;

        for (int i = 0; i < scannedFiles.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var file = scannedFiles[i];
            var normalizedPath = file.FullPath.Replace('\\', '/').TrimEnd('/');
            scannedPaths.Add(normalizedPath);

            await _indexingService.IndexFileAsync(file.FullPath, library.OwnerId, ct, libraryId);
            indexed++;

            if (i % 10 == 0 || i == scannedFiles.Count - 1)
            {
                var pct = 10 + (int)((double)(i + 1) / Math.Max(total, 1) * 80);
                await writer.WriteAsync(new ScanProgressUpdate(
                    $"Indexed {indexed}/{total}: {file.FileName}",
                    pct, total, indexed, 0, false), ct);
            }
        }

        // Mark missing files as offline
        var offlinePaths = existingPaths.Except(scannedPaths).ToList();
        int markedOffline = 0;

        if (offlinePaths.Count > 0)
        {
            await writer.WriteAsync(new ScanProgressUpdate(
                "Checking for missing files...", 92, total, indexed, 0, false), ct);

            var offlineAssets = await _dbContext.Assets
                .Where(a => a.ExternalLibraryId == libraryId
                         && a.DeletedAt == null
                         && offlinePaths.Contains(a.FullPath))
                .ToListAsync(ct);

            foreach (var asset in offlineAssets)
            {
                asset.IsOffline = true;
                markedOffline++;
            }

            await _dbContext.SaveChangesAsync(ct);
        }

        // Update library stats
        var prevTotal = existingPaths.Count;
        library.LastScannedAt = DateTime.UtcNow;
        library.LastScanStatus = ExternalLibraryScanStatus.Completed;
        library.LastScanAssetsFound = total;
        library.LastScanAssetsAdded = scannedPaths.Except(existingPaths).Count();
        library.LastScanAssetsRemoved = markedOffline;
        await _dbContext.SaveChangesAsync(ct);

        await writer.WriteAsync(new ScanProgressUpdate(
            $"Scan complete. {indexed} indexed, {markedOffline} marked offline.",
            100, total, indexed, markedOffline, true), ct);
    }
}
