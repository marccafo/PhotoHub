using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace PhotoHub.Blazor.Shared.Services;

public class UserService : IUserService
{
    private readonly HttpClient _httpClient;
    private readonly Func<Task<string?>>? _getTokenFunc;

    public UserService(HttpClient httpClient, Func<Task<string?>>? getTokenFunc = null)
    {
        _httpClient = httpClient;
        _getTokenFunc = getTokenFunc;
    }

    private async Task SetAuthHeaderAsync()
    {
        string? token = null;
        if (_getTokenFunc != null)
        {
            token = await _getTokenFunc();
        }

        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.GetFromJsonAsync<List<UserDto>>("/api/users");
        return response ?? new List<UserDto>();
    }

    public async Task<UserDto> GetUserAsync(int id)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.GetFromJsonAsync<UserDto>($"/api/users/{id}");
        return response ?? throw new Exception("User not found");
    }

    public async Task<UserDto> GetCurrentUserAsync()
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.GetFromJsonAsync<UserDto>("/api/users/me");
        return response ?? throw new Exception("User not found");
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PostAsJsonAsync("/api/users", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>() ?? throw new Exception("Failed to create user");
    }

    public async Task<UserDto> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.PutAsJsonAsync($"/api/users/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>() ?? throw new Exception("Failed to update user");
    }

    public async Task DeleteUserAsync(int id)
    {
        await SetAuthHeaderAsync();
        var response = await _httpClient.DeleteAsync($"/api/users/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(int id, string newPassword)
    {
        await SetAuthHeaderAsync();
        var request = new ResetPasswordRequest { NewPassword = newPassword };
        var response = await _httpClient.PostAsJsonAsync($"/api/users/{id}/reset-password", request);
        response.EnsureSuccessStatusCode();
    }
}
