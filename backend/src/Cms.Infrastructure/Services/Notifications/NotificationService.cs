using Cms.Application.Interfaces;
using Cms.Application.Interfaces.Notifications;

namespace Cms.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationChannelFactory _channelFactory;

    public NotificationService(
        INotificationChannelFactory channelFactory)
    {
        _channelFactory = channelFactory;
    }

    public async Task SendAsync(
       Guid userId,
       string title,
       string message,
       string channel,
       string idempotencyKey,
       CancellationToken cancellationToken = default)
    {
        var notificationChannel =
            _channelFactory.GetChannel(channel);

        await notificationChannel.SendAsync(
            userId,
            title,
            message,
            idempotencyKey,
            cancellationToken);
    }
}