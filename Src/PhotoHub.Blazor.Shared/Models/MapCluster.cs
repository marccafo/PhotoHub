namespace PhotoHub.Blazor.Shared.Models;

public class MapCluster
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Count { get; set; }
    public List<int> AssetIds { get; set; } = new();
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
    public int? FirstAssetId { get; set; }
    public bool HasThumbnail { get; set; }
    
    public string ThumbnailUrl => FirstAssetId.HasValue && HasThumbnail 
        ? $"/api/assets/{FirstAssetId.Value}/thumbnail?size=Medium" 
        : string.Empty;
}
