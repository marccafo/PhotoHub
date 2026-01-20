using PhotoHub.Blazor.Shared.Models;
using PhotoHub.Blazor.Shared.Services;

namespace PhotoHub.Blazor.WASM.Services;

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
