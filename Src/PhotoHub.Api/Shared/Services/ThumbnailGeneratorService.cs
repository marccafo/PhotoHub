using PhotoHub.API.Shared.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace PhotoHub.API.Shared.Services;

public class ThumbnailGeneratorService
{
    private readonly string _thumbnailsBasePath;
    
    public ThumbnailGeneratorService()
    {
        // Default to /thumbnails in the current directory or use environment variable
        _thumbnailsBasePath = Environment.GetEnvironmentVariable("THUMBNAILS_PATH") 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "thumbnails");
        
        // Ensure directory exists
        if (!Directory.Exists(_thumbnailsBasePath))
        {
            Directory.CreateDirectory(_thumbnailsBasePath);
        }
    }
    
    /// <summary>
    /// Generates thumbnails for an asset (Small, Medium, Large)
    /// </summary>
    public async Task<List<AssetThumbnail>> GenerateThumbnailsAsync(
        string sourceFilePath, 
        int assetId,
        CancellationToken cancellationToken = default)
    {
        var thumbnails = new List<AssetThumbnail>();
        
        try
        {
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (!IsImageFile(extension))
                return thumbnails;
            
            using var sourceImage = await Image.LoadAsync(sourceFilePath, cancellationToken);
            
            // Get EXIF orientation if available
            var orientation = GetImageOrientation(sourceImage);
            
            // Generate thumbnails for each size
            var sizes = new[] { ThumbnailSize.Small, ThumbnailSize.Medium, ThumbnailSize.Large };
            
            foreach (var size in sizes)
            {
                var thumbnail = await GenerateThumbnailAsync(
                    sourceImage, 
                    assetId, 
                    size, 
                    orientation,
                    cancellationToken);
                
                if (thumbnail != null)
                {
                    thumbnails.Add(thumbnail);
                }
            }
        }
        catch
        {
            // Return partial results if some thumbnails failed
        }
        
        return thumbnails;
    }
    
    private async Task<AssetThumbnail?> GenerateThumbnailAsync(
        Image sourceImage,
        int assetId,
        ThumbnailSize size,
        int orientation,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetSize = (int)size;
            
            // Calculate dimensions maintaining aspect ratio
            var (width, height) = CalculateThumbnailDimensions(
                sourceImage.Width, 
                sourceImage.Height, 
                targetSize,
                orientation);
            
            // Create thumbnail
            using var thumbnail = sourceImage.Clone(ctx =>
            {
                // Apply orientation transformation if needed
                if (orientation != 1)
                {
                    ApplyOrientation(ctx, orientation);
                }
                
                // Resize maintaining aspect ratio
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(width, height),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                });
            });
            
            // Save thumbnail
            var thumbnailPath = GetThumbnailPath(assetId, size);
            var thumbnailDir = Path.GetDirectoryName(thumbnailPath);
            if (!string.IsNullOrEmpty(thumbnailDir) && !Directory.Exists(thumbnailDir))
            {
                Directory.CreateDirectory(thumbnailDir);
            }
            
            var encoder = new JpegEncoder { Quality = 85 };
            await thumbnail.SaveAsync(thumbnailPath, encoder, cancellationToken);
            
            var fileInfo = new FileInfo(thumbnailPath);
            
            return new AssetThumbnail
            {
                AssetId = assetId,
                Size = size,
                FilePath = thumbnailPath,
                Width = width,
                Height = height,
                FileSize = fileInfo.Length,
                Format = "JPEG"
            };
        }
        catch
        {
            return null;
        }
    }
    
    private (int width, int height) CalculateThumbnailDimensions(
        int originalWidth, 
        int originalHeight, 
        int targetSize,
        int orientation)
    {
        // Handle orientation (swap dimensions if rotated)
        var width = originalWidth;
        var height = originalHeight;
        
        if (orientation >= 5 && orientation <= 8)
        {
            // 90 or 270 degree rotation - swap dimensions
            (width, height) = (height, width);
        }
        
        // Calculate aspect ratio
        var aspectRatio = (double)width / height;
        
        int newWidth, newHeight;
        
        if (width > height)
        {
            newWidth = targetSize;
            newHeight = (int)(targetSize / aspectRatio);
        }
        else
        {
            newHeight = targetSize;
            newWidth = (int)(targetSize * aspectRatio);
        }
        
        return (newWidth, newHeight);
    }
    
    private int GetImageOrientation(Image image)
    {
        try
        {
            // Try to get orientation from EXIF metadata
            if (image.Metadata.ExifProfile != null)
            {
                var orientationTag = image.Metadata.ExifProfile.Values
                    .FirstOrDefault(v => v.Tag == SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation);
                
                if (orientationTag != null && orientationTag.GetValue() is ushort orientationValue)
                {
                    return orientationValue;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return 1; // Default: no rotation
    }
    
    private void ApplyOrientation(IImageProcessingContext ctx, int orientation)
    {
        // Apply rotation/transformation based on EXIF orientation
        switch (orientation)
        {
            case 3: // 180 degrees
                ctx.Rotate(180);
                break;
            case 6: // 90 degrees clockwise
                ctx.Rotate(90);
                break;
            case 8: // 90 degrees counter-clockwise
                ctx.Rotate(-90);
                break;
            case 2: // Flip horizontal
                ctx.Flip(FlipMode.Horizontal);
                break;
            case 4: // Flip vertical
                ctx.Flip(FlipMode.Vertical);
                break;
            case 5: // Rotate 90 CW and flip horizontal
                ctx.Rotate(90).Flip(FlipMode.Horizontal);
                break;
            case 7: // Rotate 90 CCW and flip horizontal
                ctx.Rotate(-90).Flip(FlipMode.Horizontal);
                break;
        }
    }
    
    private string GetThumbnailPath(int assetId, ThumbnailSize size)
    {
        // Organize thumbnails by asset ID: thumbnails/{assetId}/{size}.jpg
        var sizeName = size.ToString().ToLowerInvariant();
        return Path.Combine(_thumbnailsBasePath, assetId.ToString(), $"{sizeName}.jpg");
    }
    
    /// <summary>
    /// Checks if a thumbnail file exists physically on disk
    /// </summary>
    public bool ThumbnailExists(int assetId, ThumbnailSize size)
    {
        var thumbnailPath = GetThumbnailPath(assetId, size);
        return File.Exists(thumbnailPath);
    }
    
    /// <summary>
    /// Verifies all thumbnails for an asset exist, returns missing sizes
    /// </summary>
    public List<ThumbnailSize> GetMissingThumbnailSizes(int assetId)
    {
        var sizes = new[] { ThumbnailSize.Small, ThumbnailSize.Medium, ThumbnailSize.Large };
        var missing = new List<ThumbnailSize>();
        
        foreach (var size in sizes)
        {
            if (!ThumbnailExists(assetId, size))
            {
                missing.Add(size);
            }
        }
        
        return missing;
    }
    
    private bool IsImageFile(string extension)
    {
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp", ".heic", ".heif" };
        return imageExtensions.Contains(extension);
    }
}
