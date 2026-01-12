using Microsoft.AspNetCore.Mvc;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Features.Settings;

public class SettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings/{key}", GetSetting)
            .WithName("GetSetting")
            .WithTags("Settings")
            .WithDescription("Gets a setting value by key");

        app.MapPost("/api/settings", SaveSetting)
            .WithName("SaveSetting")
            .WithTags("Settings")
            .WithDescription("Saves or updates a setting");
            
        app.MapGet("/api/settings/assets-path", GetAssetsPath)
            .WithName("GetAssetsPath")
            .WithTags("Settings")
            .WithDescription("Gets the current configured assets path");
    }

    private async Task<IResult> GetSetting(
        [FromRoute] string key,
        [FromServices] SettingsService settingsService)
    {
        var value = await settingsService.GetSettingAsync(key);
        return Results.Ok(new { key, value });
    }

    private async Task<IResult> SaveSetting(
        [FromBody] SaveSettingRequest request,
        [FromServices] SettingsService settingsService)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return Results.BadRequest("Key is required");

        await settingsService.SetSettingAsync(request.Key, request.Value ?? "");
        return Results.Ok(new { message = "Setting saved successfully" });
    }

    private async Task<IResult> GetAssetsPath(
        [FromServices] SettingsService settingsService)
    {
        var path = await settingsService.GetAssetsPathAsync();
        return Results.Ok(new { path });
    }
}

public class SaveSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
