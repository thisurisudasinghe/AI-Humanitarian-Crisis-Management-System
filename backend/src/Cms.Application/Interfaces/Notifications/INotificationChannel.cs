namespace Cms.Application.Interfaces.Notifications;

public interface INotificationChannel
{
    Task SendAsync(
        Guid userId,
        string title,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}