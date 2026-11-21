using System.Text.RegularExpressions;

namespace Cms.Infrastructure.Services.Notifications;

/// <summary>
/// Server-side notification templates. Only whitelisted placeholders are rendered,
/// which keeps names, phone numbers and exact addresses out of notification payloads.
/// </summary>
public sealed class NotificationTemplateRenderer
{
    private static readonly Regex PlaceholderPattern = new(@"\{([a-zA-Z0-9_]+)\}", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, (string Subject, string Body)> Templates =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Request.Submitted"] = (
                "Request received",
                "Your request {requestId} was submitted and is awaiting human review."),
            ["Request.StatusChanged"] = (
                "Request update",
                "The status of request {requestId} is now {status}."),
            ["Recommendation.DecisionMade"] = (
                "Decision recorded",
                "A decision ({outcome}) was recorded for recommendation {recommendationId}."),
            ["Announcement.Approved"] = (
                "Emergency announcement",
                "{title}. {message}"),
            ["Announcement.Rejected"] = (
                "Announcement update",
                "Your announcement {announcementId} was not approved."),
            ["DeadLetter.RetryQueued"] = (
                "Delivery retry queued",
                "Failed delivery {messageId} was queued for retry.")
        };

    public (string Subject, string Body) Render(
        string templateKey,
        IReadOnlyDictionary<string, string> data,
        string? language = null)
    {
        if (string.IsNullOrWhiteSpace(templateKey) ||
            !Templates.TryGetValue(templateKey, out var template))
        {
            throw new ArgumentException($"Unknown notification template: {templateKey}", nameof(templateKey));
        }

        return (template.Subject, RenderPlaceholders(template.Body, data));
    }

    public bool IsKnownTemplate(string templateKey) =>
        !string.IsNullOrWhiteSpace(templateKey) && Templates.ContainsKey(templateKey);

    private static string RenderPlaceholders(
        string body,
        IReadOnlyDictionary<string, string> data)
    {
        return PlaceholderPattern.Replace(body, match =>
        {
            var key = match.Groups[1].Value;
            return data.TryGetValue(key, out var value) ? Trim(value) : string.Empty;
        });
    }

    private static string Trim(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        value = Regex.Replace(value, @"[\r\n]+", " ").Trim();
        return value.Length <= 500 ? value : value[..500];
    }
}
