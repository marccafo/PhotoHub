using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;
using Scalar.AspNetCore;

namespace PhotoHub.API.Features.Map;

public class MapAssetsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assets/map", Handle)
            .CodeSample(
                codeSample: "curl -X GET \"http://localhost:5000/api/assets/map?zoom=10&bounds=40.0,-3.0,41.0,-2.0\" -H \"Accept: application/json\"",
                label: "cURL Example")
            .WithName("GetMapAssets")
            .WithTags("Assets")
            .WithDescription("Gets assets with GPS coordinates for map visualization")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Get map assets";
                operation.Description = "Returns assets with GPS coordinates, optionally filtered by zoom level and map bounds for clustering.";
                return Task.CompletedTask;
            });
    }

    private async Task<IResult> Handle(
        [FromServices] ApplicationDbContext dbContext,
        [FromQuery] int? zoom,
        [FromQuery] double? minLat,
        [FromQuery] double? minLng,
        [FromQuery] double? maxLat,
        [FromQuery] double? maxLng,
        CancellationToken cancellationToken)
    {
        try
        {
            // Obtener assets con coordenadas GPS
            var query = dbContext.Assets
                .Include(a => a.Exif)
                .Where(a => a.Exif != null && 
                           a.Exif.Latitude.HasValue && 
                           a.Exif.Longitude.HasValue);

            // Filtrar por bounds si se proporcionan
            if (minLat.HasValue && minLng.HasValue && maxLat.HasValue && maxLng.HasValue)
            {
                query = query.Where(a => 
                    a.Exif!.Latitude >= minLat.Value &&
                    a.Exif.Latitude <= maxLat.Value &&
                    a.Exif.Longitude >= minLng.Value &&
                    a.Exif.Longitude <= maxLng.Value);
            }

            var assets = await query
                .Select(a => new AssetLocation
                {
                    Id = a.Id,
                    CreatedDate = a.CreatedDate,
                    Latitude = a.Exif!.Latitude!.Value,
                    Longitude = a.Exif.Longitude!.Value
                })
                .ToListAsync(cancellationToken);

            // Agrupar assets en clusters basados en zoom level
            var clusterDistance = GetClusterDistance(zoom ?? 10);
            var clusters = CreateClusters(assets, clusterDistance);

            var response = clusters.Select(c => new MapClusterResponse
            {
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                Count = c.Count,
                AssetIds = c.AssetIds,
                EarliestDate = c.EarliestDate,
                LatestDate = c.LatestDate
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private double GetClusterDistance(int zoom)
    {
        // Distancia de clustering basada en zoom level (en kilómetros)
        // Zoom más alto = menor distancia = más clusters
        // Zoom más bajo = mayor distancia = menos clusters
        if (zoom <= 3)
            return 50.0; // 50 km
        if (zoom <= 6)
            return 10.0; // 10 km
        if (zoom <= 10)
            return 5.0; // 5 km
        if (zoom <= 13)
            return 1.0; // 1 km
        return 0.5; // 500 m para zoom alto
    }

    private List<MapCluster> CreateClusters(
        List<AssetLocation> assets, 
        double clusterDistance)
    {
        var clusters = new List<MapCluster>();
        var processed = new HashSet<int>();

        foreach (var asset in assets)
        {
            if (processed.Contains(asset.Id))
                continue;

            var cluster = new MapCluster
            {
                Latitude = asset.Latitude,
                Longitude = asset.Longitude,
                Count = 1,
                AssetIds = new List<int> { asset.Id },
                EarliestDate = asset.CreatedDate,
                LatestDate = asset.CreatedDate
            };

            // Buscar assets cercanos para agrupar
            foreach (var otherAsset in assets)
            {
                if (processed.Contains(otherAsset.Id) || asset.Id == otherAsset.Id)
                    continue;

                var distance = CalculateDistance(
                    asset.Latitude, asset.Longitude,
                    otherAsset.Latitude, otherAsset.Longitude);

                if (distance <= clusterDistance)
                {
                    cluster.Count++;
                    cluster.AssetIds.Add(otherAsset.Id);
                    processed.Add(otherAsset.Id);

                    // Actualizar centro del cluster (promedio ponderado)
                    var totalCount = cluster.Count;
                    cluster.Latitude = (cluster.Latitude * (totalCount - 1) + otherAsset.Latitude) / totalCount;
                    cluster.Longitude = (cluster.Longitude * (totalCount - 1) + otherAsset.Longitude) / totalCount;

                    // Actualizar fechas
                    if (otherAsset.CreatedDate < cluster.EarliestDate)
                        cluster.EarliestDate = otherAsset.CreatedDate;
                    if (otherAsset.CreatedDate > cluster.LatestDate)
                        cluster.LatestDate = otherAsset.CreatedDate;
                }
            }

            processed.Add(asset.Id);
            clusters.Add(cluster);
        }

        return clusters;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Fórmula de Haversine para calcular distancia entre dos puntos GPS
        const double R = 6371.0; // Radio de la Tierra en km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private class AssetLocation
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private class MapCluster
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
        public List<int> AssetIds { get; set; } = new();
        public DateTime EarliestDate { get; set; }
        public DateTime LatestDate { get; set; }
    }
}
