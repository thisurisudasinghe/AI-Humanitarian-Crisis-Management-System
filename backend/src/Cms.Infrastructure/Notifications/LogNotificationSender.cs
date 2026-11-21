using Cms.Application.Contracts;
using Cms.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Development/test email sender used when <c>Smtp:Host</c> is not configured.
/// It records the delivery attempt in the log instead of contacting a mail
/// server, so a fresh checkout can exercise M9 without SMTP credentials.
/// </summary>
public sealed class LogNotificationSender : INotificationSender
{
    private readonly ILogger<LogNotificationSender> _logger;

    public LogNotificationSender(ILogger<LogNotificationSender> logger)
    {
        _logger = logger;
    }

    public string Channel => "Email";

    public Task SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _logger.LogInformation(
            "SMTP is not configured. Notification {NotificationId} ({TemplateKey}) for " +
            "recipient {RecipientUserId} was recorded without an external email. Subject: {Subject}",
            notification.Id,
            notification.TemplateKey,
            notification.RecipientUserId,
            notification.Subject);

        return Task.CompletedTask;
    }
}
