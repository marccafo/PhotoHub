using PhotoHub.API.Features.ScanPhotos;

using Microsoft.AspNetCore.OpenApi;

namespace PhotoHub.API.Features.ScanPhotos;

public static class ScanPhotosEndpoint
{
    public static void MapScanPhotosEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/photos/scan", async (
            string directoryPath,
            ScanPhotosHandler handler,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var photos = await handler.HandleAsync(directoryPath, cancellationToken);
                return Results.Ok(photos);
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithName("ScanPhotos")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Scans a directory and returns the list of found photographs";
            operation.Description = "This endpoint recursively scans a specified directory and returns a list of all found photographs. Only files with extensions: .jpg, .jpeg, .png, .bmp, .tiff are included";
            return Task.CompletedTask;
        });
    }
}

