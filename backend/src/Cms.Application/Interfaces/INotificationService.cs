using Cms.Application.DTOs;
using Cms.Application.DTOs.Notifications;

namespace Cms.Application.Interfaces;

/// <summary>In-app notification inbox and the admin failed-delivery view (M9).</summary>
public interface INotificationService
{
    Task<NotificationInboxDto> GetInboxAsync(
        Guid recipientUserId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default);

    Task<NotificationDto> MarkReadAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<NotificationDto>> GetFailedAsync(
        NotificationQueryDto query,
        CancellationToken cancellationToken = default);
}
