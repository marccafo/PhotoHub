using System.Net.Http.Json;

namespace PhotoHub.Blazor.Shared.Services;

public class SettingsService : ISettingsService
{
    private readonly HttpClient _httpClient;

    public SettingsService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<SettingResponse>($"/api/settings/{key}");
            return response?.Value ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task<bool> SaveSettingAsync(string key, string value)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/settings", new { Key = key, Value = value });
        return response.IsSuccessStatusCode;
    }

    public async Task<string> GetAssetsPathAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<AssetsPathResponse>("/api/settings/assets-path");
            return response?.Path ?? "";
        }
        catch
        {
            return "";
        }
    }

    private class SettingResponse
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    private class AssetsPathResponse
    {
        public string Path { get; set; } = string.Empty;
    }
}
