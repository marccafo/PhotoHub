using PhotoHub.Client.Shared.Models;

namespace PhotoHub.Client.Shared.Services;

public interface IPendingAssetsProvider
{
    Task<List<TimelineItem>> GetPendingAssetsAsync();
    Task<AssetDetail?> GetPendingAssetDetailAsync(string path);
}
