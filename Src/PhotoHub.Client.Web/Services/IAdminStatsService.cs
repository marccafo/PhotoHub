using PhotoHub.Client.Web.Models;

namespace PhotoHub.Client.Web.Services;

public interface IAdminStatsService
{
    Task<AdminStatsResponse> GetStatsAsync();
}
