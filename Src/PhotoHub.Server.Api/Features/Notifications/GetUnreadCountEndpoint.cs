using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PhotoHub.Server.Api.Shared.Interfaces;
using PhotoHub.Server.Api.Shared.Services;

namespace PhotoHub.Server.Api.Features.Notifications;

public class GetUnreadCountEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications/unread-count", Handle)
            .WithName("GetUnreadNotificationsCount")
            .WithTags("Notifications")
            .RequireAuthorization();
    }

    private static async Task<IResult> Handle(
        [FromServices] INotificationService notificationService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!Guid.TryParse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return Results.Unauthorized();

        var count = await notificationService.GetUnreadCountAsync(userId);
        return Results.Ok(new { Count = count });
    }
}
