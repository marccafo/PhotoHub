using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IPendingAssetsProvider
{
    Task<List<TimelineItem>> GetPendingAssetsAsync();
    Task<AssetDetail?> GetPendingAssetDetailAsync(string path);
}
