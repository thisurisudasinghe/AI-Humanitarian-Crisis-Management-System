using Cms.Application.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Services.Notifications;

public class EmailChannel : INotificationChannel
{
    private readonly ILogger<EmailChannel> _logger;

    public EmailChannel(ILogger<EmailChannel> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(
     Guid userId,
     string title,
     string message,
     string idempotencyKey,
     CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Email sending is not configured yet for user {UserId}.",
            userId);

        return Task.CompletedTask;
    }
}