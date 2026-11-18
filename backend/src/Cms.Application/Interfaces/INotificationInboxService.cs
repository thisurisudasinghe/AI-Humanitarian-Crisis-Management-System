using Cms.Application.DTOs.Common;
using Cms.Application.DTOs.Notifications;

namespace Cms.Application.Interfaces;

public interface INotificationInboxService
{
    Task<PagedResult<NotificationDto>> GetInboxAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}
