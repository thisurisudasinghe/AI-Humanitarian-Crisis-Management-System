namespace Cms.Application.Interfaces;

public interface INotificationService
{
    Task SendAsync(
        Guid userId,
        string title,
        string message,
        string channel,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders an approved notification template and sends it through the requested channel.
    /// </summary>
    Task SendTemplateAsync(
        Guid userId,
        string templateKey,
        string channel,
        string idempotencyKey,
        IReadOnlyDictionary<string, string> data,
        string? language = null,
        CancellationToken cancellationToken = default);
}