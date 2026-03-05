using PhotoHub.Client.Shared.Models;

namespace PhotoHub.Client.Shared.Services;

public interface IMapService
{
    Task<List<MapCluster>> GetMapClustersAsync(int? zoom = null, double? minLat = null, double? minLng = null, double? maxLat = null, double? maxLng = null);
}
