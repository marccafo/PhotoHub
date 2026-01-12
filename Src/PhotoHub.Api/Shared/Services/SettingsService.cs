using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Services;

public class SettingsService
{
    private readonly ApplicationDbContext _dbContext;
    public const string AssetsPathKey = "AssetsPath";

    public SettingsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value ?? defaultValue;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var setting = await _dbContext.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new Setting { Key = key, Value = value, UpdatedAt = DateTime.UtcNow };
            _dbContext.Settings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
            _dbContext.Settings.Update(setting);
        }
        await _dbContext.SaveChangesAsync();
    }

    public async Task<string> GetAssetsPathAsync()
    {
        var path = await GetSettingAsync(AssetsPathKey);
        
        if (string.IsNullOrEmpty(path))
        {
            path = Environment.GetEnvironmentVariable("ASSETS_PATH") 
                   ?? Path.Combine(Directory.GetCurrentDirectory(), "assets");

            // Use the container path if running in Docker
            if (Directory.Exists("/assets"))
            {
                path = "/assets";
            }
        }
        
        return path;
    }
}
