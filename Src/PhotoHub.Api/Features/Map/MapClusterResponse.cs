namespace PhotoHub.API.Features.Map;

public class MapClusterResponse
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Count { get; set; }
    public List<int> AssetIds { get; set; } = new();
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
}
