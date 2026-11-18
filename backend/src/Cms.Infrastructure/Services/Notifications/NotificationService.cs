using Cms.Application.Interfaces;
using Cms.Application.Interfaces.Notifications;

namespace Cms.Infrastructure.Services.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationChannelFactory _channelFactory;
    private readonly NotificationTemplateRenderer _templateRenderer;

    public NotificationService(
        INotificationChannelFactory channelFactory,
        NotificationTemplateRenderer templateRenderer)
    {
        _channelFactory = channelFactory;
        _templateRenderer = templateRenderer;
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

    public async Task SendTemplateAsync(
        Guid userId,
        string templateKey,
        string channel,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> data,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        var (subject, body) = _templateRenderer.Render(templateKey, data, language);

        await SendAsync(
            userId,
            subject,
            body,
            channel,
            idempotencyKey,
            cancellationToken);
    }
}