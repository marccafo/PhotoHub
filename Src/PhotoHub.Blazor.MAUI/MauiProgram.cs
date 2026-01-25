using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using PhotoHub.Blazor.Shared.Services;
using PhotoHub.Blazor.MAUI.Services;

namespace PhotoHub.Blazor.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Agregar MudBlazor
        builder.Services.AddMudServices();

        // Configurar HttpClient
        var apiBaseUrl = "http://10.0.2.2:5178"; // Android Emulator address for localhost
#if IOS || MACCATALYST || WINDOWS
        apiBaseUrl = "http://localhost:5178";
#endif

        builder.Services.AddScoped<ApiErrorNotifier>();
        builder.Services.AddScoped(sp =>
        {
            var notifier = sp.GetRequiredService<ApiErrorNotifier>();
            var refreshHandler = new AuthRefreshHandler(() => sp.GetRequiredService<IAuthService>())
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

        // Registrar servicios
        builder.Services.AddScoped<LayoutService>();
        builder.Services.AddScoped<ThemeService>();
        builder.Services.AddScoped<IAuthService, MauiAuthService>();
        builder.Services.AddScoped<MauiAuthService>(sp => 
            (MauiAuthService)sp.GetRequiredService<IAuthService>());
        
        builder.Services.AddScoped<IAssetService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new AssetService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IIndexService, IndexService>();
        
        builder.Services.AddScoped<IFolderService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new FolderService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IPendingAssetsProvider, MauiPendingAssetsProvider>();
        builder.Services.AddScoped<IMapService, MapService>();
        
        builder.Services.AddScoped<IAlbumService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new AlbumService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<ISettingsService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new PhotoHub.Blazor.Shared.Services.SettingsService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IUserService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new UserService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IAlbumPermissionService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new AlbumPermissionService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IFolderPermissionService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new FolderPermissionService(httpClient, async () => await authService.GetTokenAsync());
        });
        
        builder.Services.AddScoped<IAdminStatsService>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var authService = sp.GetRequiredService<IAuthService>();
            return new AdminStatsService(httpClient, async () => await authService.GetTokenAsync());
        });

        builder.Services.AddMudBlazorDialog();
        builder.Services.AddMudBlazorSnackbar();

        return builder.Build();
    }
}