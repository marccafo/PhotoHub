using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;

namespace PhotoHub.API.Features.Timeline;

public class TimelineEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/photos/timeline", Handle)
        .WithName("GetTimeline")
        .WithTags("Photos")
        .WithDescription("Gets the timeline of all scanned photos")
        .AddOpenApiOperationTransformer((operation, context, ct) =>
        {
            operation.Summary = "Gets the timeline";
            operation.Description = "Returns all photos stored in the database, ordered by the most recently scanned first, then by modification date";
            return Task.CompletedTask;
        });
    }

    private async Task<IResult> Handle(
        [FromServices] PhotoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var photos = await dbContext.Photos
            .OrderByDescending(p => p.ScannedAt)
            .ThenByDescending(p => p.ModifiedDate)
            .ToListAsync(cancellationToken);

            var finalPhotos = photos.Select(photo => new TimelineResponse
            {
                Id = photo.Id,
                FileName = photo.FileName,
                FullPath = photo.FullPath,
                FileSize = photo.FileSize,
                CreatedDate = ConvertUtcToLocal(photo.CreatedDate, photo.TimeZoneOffsetMinutes),
                ModifiedDate = ConvertUtcToLocal(photo.ModifiedDate, photo.TimeZoneOffsetMinutes),
                Extension = photo.Extension,
                ScannedAt = photo.ScannedAt // ScannedAt siempre es UTC, no necesita conversión
            });

            return Results.Ok(finalPhotos);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }

    private DateTime ConvertUtcToLocal(DateTime utcDateTime, int offsetMinutes)
    {
        return utcDateTime.AddMinutes(offsetMinutes);
    }
}

