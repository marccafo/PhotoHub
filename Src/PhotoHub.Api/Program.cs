using PhotoHub.Api;
using PhotoHub.API.Features.IndexAssets;
using PhotoHub.API.Features.Timeline;
using Scalar.AspNetCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.AddPostgres();

builder.AddApplicationServices();

var app = builder.Build();

app.ExecuteMigrations();
await app.EnsureFFmpegAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Configure static files for thumbnails
var thumbnailsPath = app.Configuration["THUMBNAILS_PATH"] 
    ?? Path.Combine(Directory.GetCurrentDirectory(), "thumbnails");

if (!Directory.Exists(thumbnailsPath))
{
    Directory.CreateDirectory(thumbnailsPath);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(thumbnailsPath),
    RequestPath = "/thumbnails"
});

app.RegisterEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
