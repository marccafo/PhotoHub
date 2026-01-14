using System.Net.Http.Json;
using Microsoft.JSInterop;
using PhotoHub.Blazor.Shared.Services;

namespace PhotoHub.Blazor.WASM.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private const string TokenKey = "authToken";
    private const string UserKey = "authUser";
    private UserDto? _currentUser;

    public event Action? OnAuthStateChanged;

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var request = new LoginRequest { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (loginResponse != null)
                {
                    await SaveTokenAsync(loginResponse.Token);
                    await SaveUserAsync(loginResponse.User);
                    _currentUser = loginResponse.User;
                    OnAuthStateChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        await RemoveTokenAsync();
        await RemoveUserAsync();
        _currentUser = null;
        OnAuthStateChanged?.Invoke();
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        if (_currentUser != null)
            return _currentUser;

        try
        {
            var userJson = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", UserKey);
            if (!string.IsNullOrEmpty(userJson))
            {
                // Necesitaríamos deserializar el JSON, pero por simplicidad lo haremos con el endpoint
                var token = await GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    var response = await _httpClient.GetFromJsonAsync<UserDto>("/api/users/me");
                    if (response != null)
                    {
                        _currentUser = response;
                        await SaveUserAsync(response);
                        return response;
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    public async Task SaveTokenAsync(string token)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
    }

    public async Task SaveUserAsync(UserDto user)
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserKey, 
            System.Text.Json.JsonSerializer.Serialize(user));
        _currentUser = user;
        OnAuthStateChanged?.Invoke();
    }

    private async Task RemoveTokenAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
    }

    private async Task RemoveUserAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserKey);
    }
}
