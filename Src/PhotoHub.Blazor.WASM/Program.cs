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
builder.Services.AddScoped(sp => new HttpClient 
{ 
    BaseAddress = new Uri(apiBaseUrl) 
});

// Agregar MudBlazor
builder.Services.AddMudServices();

// Agregar servicios personalizados
builder.Services.AddScoped<LayoutService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IIndexService, IndexService>();
builder.Services.AddScoped<IFolderService, FolderService>();
builder.Services.AddScoped<IMapService, MapService>();
builder.Services.AddScoped<ISettingsService, PhotoHub.Blazor.Shared.Services.SettingsService>();

await builder.Build().RunAsync();