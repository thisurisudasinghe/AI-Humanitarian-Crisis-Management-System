using Cms.Application.Contracts;
using Cms.Domain.Constants;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Resolves the concrete M9 channels from the frozen channel names in the
/// Notification.Send payload. Adding a future channel means adding one
/// <see cref="INotificationChannel"/> implementation and registering it here.
/// </summary>
public sealed class NotificationChannelFactory : INotificationChannelFactory
{
    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;

    public NotificationChannelFactory(
        InAppNotificationChannel inApp,
        EmailNotificationChannel email)
    {
        _channels = new Dictionary<string, INotificationChannel>(StringComparer.OrdinalIgnoreCase)
        {
            [NotificationChannels.InApp] = inApp,
            [NotificationChannels.Email] = email
        };
    }

    public IReadOnlyList<INotificationChannel> GetChannels(string? requestedChannel)
    {
        var channel = requestedChannel?.Trim();

        if (string.IsNullOrEmpty(channel))
        {
            return _channels.Values.ToList();
        }

        if (_channels.TryGetValue(channel, out var resolved))
        {
            return new[] { resolved };
        }

        throw new InvalidOperationException(
            $"Unsupported notification channel '{requestedChannel}'.");
    }
}
