namespace PhotoHub.API.Features.ScanAssets;

public class ScanAssetsResponse
{
    public PhotoHub.Blazor.Shared.Models.ScanStatistics Statistics { get; set; } = null!;
    public int AssetsProcessed { get; set; }
    public string Message { get; set; } = string.Empty;
}

