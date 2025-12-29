using PhotoHub.Api;
using PhotoHub.API.Features.ScanAssets;
using PhotoHub.API.Features.Timeline;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.AddPostgres();

builder.AddApplicationServices();

var app = builder.Build();

app.ExecuteMigrations();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.RegisterEndpoints();

app.Run();
