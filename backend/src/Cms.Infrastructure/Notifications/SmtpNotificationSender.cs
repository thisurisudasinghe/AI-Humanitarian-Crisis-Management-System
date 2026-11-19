using Cms.Application.Contracts;
using Cms.Domain.Entities;
using Cms.Infrastructure.Security;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// SMTP delivery via MailKit (C3-04). The recipient address is decrypted at send
/// time and never stored in plaintext on the notification row.
/// </summary>
public sealed class SmtpNotificationSender : INotificationSender
{
    private readonly SmtpOptions _options;
    private readonly EmailProtectionService _emailProtection;
    private readonly ILogger<SmtpNotificationSender> _logger;

    public SmtpNotificationSender(
        SmtpOptions options,
        EmailProtectionService emailProtection,
        ILogger<SmtpNotificationSender> logger)
    {
        _options = options;
        _emailProtection = emailProtection;
        _logger = logger;
    }

    public string Channel => "Email";

    public async Task SendAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var encrypted = notification.User?.EmailEncrypted;

        if (string.IsNullOrWhiteSpace(encrypted))
        {
            throw new InvalidOperationException(
                $"Notification {notification.Id} has no recipient email address.");
        }

        var recipient = _emailProtection.DecryptEmail(encrypted);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = string.IsNullOrWhiteSpace(notification.Subject)
            ? notification.TemplateKey
            : notification.Subject;
        message.Body = new TextPart("plain")
        {
            Text = notification.Body ?? string.Empty
        };

        using var client = new SmtpClient
        {
            Timeout = Math.Max(1, _options.TimeoutSeconds) * 1000
        };

        await client.ConnectAsync(
            _options.Host,
            _options.Port,
            _options.UseStartTls
                ? SecureSocketOptions.StartTlsWhenAvailable
                : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            await client.AuthenticateAsync(
                _options.Username,
                _options.Password ?? string.Empty,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation(
            "Notification {NotificationId} emailed to recipient {RecipientUserId} via {SmtpHost}.",
            notification.Id,
            notification.RecipientUserId,
            _options.Host);
    }
}
