using System.Net.Http.Json;
using PhotoHub.Client.Shared.Services;

namespace PhotoHub.Client.Native.Services;

public class MauiAuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private const string TokenKey = "authToken";
    private const string RefreshTokenKey = "refreshToken";
    private const string UserKey = "authUser";
    private const string DeviceIdKey = "deviceId";
    private UserDto? _currentUser;

    public event Action? OnAuthStateChanged;

    public MauiAuthService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var deviceId = await EnsureDeviceIdAsync();
            var request = new LoginRequest { Username = username, Password = password, DeviceId = deviceId };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (loginResponse != null)
                {
                    await SaveTokenAsync(loginResponse.Token);
                    await SaveRefreshTokenAsync(loginResponse.RefreshToken);
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
        await RemoveRefreshTokenAsync();
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
            return await SecureStorage.Default.GetAsync(TokenKey);
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
            var userJson = await SecureStorage.Default.GetAsync(UserKey);
            if (!string.IsNullOrEmpty(userJson))
            {
                // Intentamos deserializar el cacheado primero
                try {
                    _currentUser = System.Text.Json.JsonSerializer.Deserialize<UserDto>(userJson);
                } catch { /* Ignore */ }

                var token = await GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    // Validamos/Refrescamos datos contra la API
                    var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    var response = await _httpClient.SendAsync(request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var userDto = await response.Content.ReadFromJsonAsync<UserDto>();
                        if (userDto != null)
                        {
                            _currentUser = userDto;
                            await SaveUserAsync(userDto);
                            return userDto;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }
        return _currentUser;
    }

    public async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var deviceId = await EnsureDeviceIdAsync();
            var request = new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                DeviceId = deviceId
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
            {
                Content = JsonContent.Create(request)
            };
            message.Options.Set(AuthRefreshHandler.SkipAuthRefresh, true);

            var response = await _httpClient.SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var refreshResponse = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>();
            if (refreshResponse == null)
            {
                return false;
            }

            await SaveTokenAsync(refreshResponse.Token);
            await SaveRefreshTokenAsync(refreshResponse.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task SaveTokenAsync(string token)
    {
        await SecureStorage.Default.SetAsync(TokenKey, token);
    }

    public async Task SaveRefreshTokenAsync(string refreshToken)
    {
        await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken);
    }

    public async Task SaveUserAsync(UserDto user)
    {
        await SecureStorage.Default.SetAsync(UserKey, 
            System.Text.Json.JsonSerializer.Serialize(user));
        _currentUser = user;
        OnAuthStateChanged?.Invoke();
    }

    private async Task RemoveTokenAsync()
    {
        SecureStorage.Default.Remove(TokenKey);
        await Task.CompletedTask;
    }

    private async Task RemoveRefreshTokenAsync()
    {
        SecureStorage.Default.Remove(RefreshTokenKey);
        await Task.CompletedTask;
    }

    private async Task RemoveUserAsync()
    {
        SecureStorage.Default.Remove(UserKey);
        await Task.CompletedTask;
    }

    private async Task<string> EnsureDeviceIdAsync()
    {
        try
        {
            var existing = await SecureStorage.Default.GetAsync(DeviceIdKey);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }
        }
        catch
        {
            // Ignore
        }

        var deviceId = Guid.NewGuid().ToString("N");
        await SecureStorage.Default.SetAsync(DeviceIdKey, deviceId);
        return deviceId;
    }
}
