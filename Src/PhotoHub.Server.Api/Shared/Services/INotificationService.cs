using PhotoHub.Server.Api.Shared.Models;

namespace PhotoHub.Server.Api.Shared.Services;

public interface INotificationService
{
    Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? actionUrl = null);
    Task<(List<Notification> Items, int TotalCount)> GetPagedAsync(Guid userId, int page, int pageSize, bool unreadOnly);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkAsReadAsync(Guid notificationId, Guid userId);
    Task MarkAllAsReadAsync(Guid userId);
}
