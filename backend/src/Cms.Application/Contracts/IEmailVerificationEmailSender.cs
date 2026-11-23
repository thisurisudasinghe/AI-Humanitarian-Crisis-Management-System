namespace Cms.Application.Contracts;

/// <summary>
/// Dedicated transactional sender for one-time verification links. This is
/// intentionally separate from INotificationSender: notification rows persist
/// rendered bodies, and a one-time token must never be stored in the database.
/// </summary>
public interface IEmailVerificationEmailSender
{
    Task SendAsync(
        string recipientEmail,
        string displayName,
        string fromName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}
