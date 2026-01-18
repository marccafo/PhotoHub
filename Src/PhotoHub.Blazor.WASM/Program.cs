using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PhotoHub.Blazor.WASM;
using MudBlazor.Services;
using PhotoHub.Blazor.Shared.Services;
using PhotoHub.Blazor.WASM.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurar HttpClient para la API
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped<ApiErrorNotifier>();
builder.Services.AddScoped(sp =>
{
    var notifier = sp.GetRequiredService<ApiErrorNotifier>();
    var refreshHandler = new AuthRefreshHandler(() => sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>())
    {
        InnerHandler = new HttpClientHandler()
    };
    var errorHandler = new ApiErrorHandler(notifier)
    {
        InnerHandler = refreshHandler
    };
    return new HttpClient(errorHandler)
    {
        BaseAddress = new Uri(apiBaseUrl)
    };
});

// Agregar MudBlazor
builder.Services.AddMudServices();

// Agregar servicios personalizados
builder.Services.AddScoped<LayoutService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<PhotoHub.Blazor.Shared.Services.IAuthService, PhotoHub.Blazor.WASM.Services.AuthService>();
builder.Services.AddScoped<PhotoHub.Blazor.WASM.Services.AuthService>(sp => 
    (PhotoHub.Blazor.WASM.Services.AuthService)sp.GetRequiredService<PhotoHub.Blazor.Shared.Services.IAuthService>());
builder.Services.AddScoped<IAssetService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new AssetService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<IIndexService, IndexService>();
builder.Services.AddScoped<IFolderService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new FolderService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<IAlbumService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new AlbumService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<ISettingsService, PhotoHub.Blazor.Shared.Services.SettingsService>();
builder.Services.AddScoped<IUserService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new UserService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<IAlbumPermissionService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new AlbumPermissionService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<IFolderPermissionService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new FolderPermissionService(httpClient, async () => await authService.GetTokenAsync());
});
builder.Services.AddScoped<IAdminStatsService>(sp =>
{
    var httpClient = sp.GetRequiredService<HttpClient>();
    var authService = sp.GetRequiredService<PhotoHub.Blazor.WASM.Services.AuthService>();
    return new AdminStatsService(httpClient, async () => await authService.GetTokenAsync());
});

await builder.Build().RunAsync();