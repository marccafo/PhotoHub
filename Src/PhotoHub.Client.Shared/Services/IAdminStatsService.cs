using PhotoHub.Client.Shared.Models;

namespace PhotoHub.Client.Shared.Services;

public interface IAdminStatsService
{
    Task<AdminStatsResponse> GetStatsAsync();
}
