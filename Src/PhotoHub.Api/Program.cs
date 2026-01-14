using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PhotoHub.Api;
using Scalar.AspNetCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configurar JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PhotoHub";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PhotoHub";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.AddPostgres();

builder.AddApplicationServices();

var app = builder.Build();

app.ExecuteMigrations();
await app.InitializeAdminUserAsync();
await app.EnsureFFmpegAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// IMPORTANTE: Authentication y Authorization deben ir antes de UseBlazorFrameworkFiles
app.UseAuthentication();
app.UseAuthorization();

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
