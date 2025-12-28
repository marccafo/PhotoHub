using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Services;

public class DirectoryScanner
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".tiff"
    };

    public async Task<IEnumerable<Photo>> ScanDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("El directorio no puede estar vacío.", nameof(directoryPath));
        }

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"El directorio '{directoryPath}' no existe.");
        }

        var photos = new List<Photo>();

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(filePath);
                if (AllowedExtensions.Contains(extension))
                {
                    var fileInfo = new FileInfo(filePath);
                    photos.Add(new Photo
                    {
                        FileName = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        FileSize = fileInfo.Length,
                        CreatedDate = fileInfo.CreationTime,
                        ModifiedDate = fileInfo.LastWriteTime,
                        Extension = extension
                    });
                }
            }
        }, cancellationToken);

        return photos;
    }
}

