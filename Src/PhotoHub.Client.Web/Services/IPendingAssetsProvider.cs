using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

public interface IPendingAssetsProvider
{
    Task<List<TimelineItem>> GetPendingAssetsAsync();
    Task<AssetDetail?> GetPendingAssetDetailAsync(string path);
}
