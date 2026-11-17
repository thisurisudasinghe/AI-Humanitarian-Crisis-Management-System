
using Cms.Application.Interfaces.Notifications;

namespace Cms.Infrastructure.Services.Notifications;

public class NotificationChannelFactory : INotificationChannelFactory
{
    private readonly EmailChannel _emailChannel;
    private readonly InAppChannel _inAppChannel;

    public NotificationChannelFactory(
        EmailChannel emailChannel,
        InAppChannel inAppChannel)
    {
        _emailChannel = emailChannel;
        _inAppChannel = inAppChannel;
    }

    public INotificationChannel GetChannel(string channel)
    {
        return channel.ToLowerInvariant() switch
        {
            "email" => _emailChannel,
            "inapp" => _inAppChannel,
            _ => throw new ArgumentException(
                $"Unsupported notification channel: {channel}")
        };
    }
}
