namespace PhotoHub.Server.Api.Features.IndexAssets;

public class IndexAssetsResponse
{
    public PhotoHub.Client.Shared.Models.IndexStatistics Statistics { get; set; } = null!;
    public int AssetsProcessed { get; set; }
    public string Message { get; set; } = string.Empty;
}

