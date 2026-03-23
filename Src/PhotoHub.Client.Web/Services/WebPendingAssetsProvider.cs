using PhotoHub.Client.Web.Models;
using PhotoHub.Client.Web.Services;

namespace PhotoHub.Client.Web.Services;

public class WebPendingAssetsProvider : IPendingAssetsProvider
{
    private readonly IAssetService _assetService;

    public WebPendingAssetsProvider(IAssetService assetService)
    {
        _assetService = assetService;
    }

    public Task<List<TimelineItem>> GetPendingAssetsAsync()
    {
        return _assetService.GetDeviceAssetsAsync();
    }

    public Task<AssetDetail?> GetPendingAssetDetailAsync(string path)
    {
        return _assetService.GetPendingAssetDetailAsync(path);
    }
}
