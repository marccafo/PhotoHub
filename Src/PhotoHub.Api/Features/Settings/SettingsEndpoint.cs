using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.Settings;

public class SettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings")
            .WithTags("Settings")
            .RequireAuthorization();

        group.MapGet("/{key}", GetSetting)
            .WithName("GetSetting")
            .WithDescription("Gets a setting value by key");

        group.MapPost("", SaveSetting)
            .WithName("SaveSetting")
            .WithDescription("Saves or updates a setting");
            
        group.MapGet("/assets-path", GetAssetsPath)
            .WithName("GetAssetsPath")
            .WithDescription("Gets the current configured assets path");
    }

    private async Task<IResult> GetSetting(
        [FromRoute] string key,
        [FromServices] SettingsService settingsService,
        ClaimsPrincipal user)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var value = await settingsService.GetSettingAsync(key, userId);
        return Results.Ok(new { key, value });
    }

    private async Task<IResult> SaveSetting(
        [FromBody] SaveSettingRequest request,
        [FromServices] SettingsService settingsService,
        ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return Results.BadRequest("Key is required");

        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        await settingsService.SetSettingAsync(request.Key, request.Value ?? "", userId);
        return Results.Ok(new { message = "Setting saved successfully" });
    }

    private async Task<IResult> GetAssetsPath(
        [FromServices] SettingsService settingsService,
        ClaimsPrincipal user)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var path = await settingsService.GetAssetsPathAsync(userId);
        return Results.Ok(new { path });
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim?.Value, out userId);
    }
}

public class SaveSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
