using Cms.Application.Interfaces.Notifications;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace Cms.Infrastructure.Services.Notifications;

public class EmailChannel : INotificationChannel
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailChannel> _logger;

    public EmailChannel(
        ApplicationDbContext context,
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<EmailChannel> logger)
    {
        _context = context;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid userId,
        string title,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Notifications.AnyAsync(
                n => n.IdempotencyKey == idempotencyKey &&
                     n.Channel == "Email",
                cancellationToken))
        {
            return;
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Channel = "Email",
            IdempotencyKey = idempotencyKey,
            Status = NotificationStatus.Pending
        };

        await _context.Notifications.AddAsync(notification, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var host = _configuration["Smtp:Host"];

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new InvalidOperationException("SMTP host is not configured.");
            }

            if (user == null || string.IsNullOrWhiteSpace(user.EmailEncrypted))
            {
                throw new InvalidOperationException(
                    "Recipient does not have a registered email address.");
            }

            var encryption = _serviceProvider.GetService(typeof(EmailProtectionService))
                as EmailProtectionService;

            if (encryption == null)
            {
                throw new InvalidOperationException(
                    "Email protection service is not configured.");
            }

            var recipient = encryption.DecryptEmail(user.EmailEncrypted);
            var from = _configuration["Smtp:From"] ?? recipient;
            var port = int.TryParse(_configuration["Smtp:Port"], out var parsed) ? parsed : 587;

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(from));
            email.To.Add(MailboxAddress.Parse(recipient));
            email.Subject = string.IsNullOrWhiteSpace(title) ? "Crisis management notification" : title;
            email.Body = new TextPart("plain") { Text = message };

            using var smtp = new SmtpClient();
            smtp.Timeout = 10_000;
            await smtp.ConnectAsync(
                host,
                port,
                SecureSocketOptions.StartTlsWhenAvailable,
                cancellationToken);

            var userLogin = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];

            if (!string.IsNullOrWhiteSpace(userLogin) &&
                !string.IsNullOrWhiteSpace(password))
            {
                await smtp.AuthenticateAsync(userLogin, password, cancellationToken);
            }

            await smtp.SendAsync(email, cancellationToken);
            await smtp.DisconnectAsync(true, cancellationToken);

            notification.Status = NotificationStatus.Sent;
            notification.SentAt = DateTime.UtcNow;
            notification.UpdatedAt = notification.SentAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            notification.Status = NotificationStatus.Failed;
            notification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Email delivery failed for notification {NotificationId}.", notification.Id);
            throw;
        }
    }
}
