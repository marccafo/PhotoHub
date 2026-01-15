using PhotoHub.Blazor.Shared.Models;

namespace PhotoHub.Blazor.Shared.Services;

public interface IAdminStatsService
{
    Task<AdminStatsResponse> GetStatsAsync();
}
