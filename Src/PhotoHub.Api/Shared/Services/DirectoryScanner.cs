using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Services;

public class DirectoryScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp", ".heic", ".heif"
    };
    
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".3gp", ".mpeg", ".mpg", ".3g2", ".3gpp", ".amv", ".asf", ".f4v", ".m2v", ".mp2", ".mpe", ".mpv", ".ogv", ".qt", ".vob"
    };
    
    private static readonly HashSet<string> AllowedExtensions = new(
        ImageExtensions.Concat(VideoExtensions), 
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Recursively scans a directory and returns all media files (images and videos)
    /// Ignores hidden files and unsupported formats
    /// </summary>
    public async Task<IEnumerable<ScannedFile>> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be empty.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory '{directoryPath}' does not exist.");
        }

        var files = new List<ScannedFile>();

        await Task.Run(() =>
        {
            try 
            {
                var enumerationOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
                };

                var allFiles = Directory.EnumerateFiles(directoryPath, "*.*", enumerationOptions);

                foreach (var filePath in allFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var extension = Path.GetExtension(filePath);
                    if (AllowedExtensions.Contains(extension))
                    {
                        var fileInfo = new FileInfo(filePath);
                        var assetType = ImageExtensions.Contains(extension) ? AssetType.IMAGE : AssetType.VIDEO;
                        
                        files.Add(new ScannedFile
                        {
                            FileName = fileInfo.Name,
                            FullPath = fileInfo.FullName,
                            FileSize = fileInfo.Length,
                            CreatedDate = fileInfo.CreationTimeUtc,
                            ModifiedDate = fileInfo.LastWriteTimeUtc,
                            Extension = extension,
                            AssetType = assetType
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error scanning directory {directoryPath}: {ex.Message}");
            }
        }, cancellationToken);

        return files;
    }
}

/// <summary>
/// Represents a scanned media file from the filesystem
/// </summary>
public class ScannedFile
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Extension { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
}

