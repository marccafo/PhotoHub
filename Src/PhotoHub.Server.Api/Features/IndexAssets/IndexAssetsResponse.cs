using PhotoHub.Server.Api.Shared.Dtos;

namespace PhotoHub.Server.Api.Features.IndexAssets;

public class IndexAssetsResponse
{
    public IndexStatistics Statistics { get; set; } = null!;
    public int AssetsProcessed { get; set; }
    public string Message { get; set; } = string.Empty;
}

