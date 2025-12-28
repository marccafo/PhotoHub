using PhotoHub.API.Shared.Services;

namespace PhotoHub.Api;

public static class DependencyInjection
{
    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DirectoryScanner>();
    }
}