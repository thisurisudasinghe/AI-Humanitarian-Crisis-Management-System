using System.Text;
using System.Text.RegularExpressions;
using Cms.Application.DTOs.Notifications;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Minimal template registry for the frozen <c>Notification.Send</c> payload.
/// A payload may override <c>Subject</c>/<c>Body</c> directly; otherwise the
/// registry supplies sensible defaults and substitutes <c>{key}</c> tokens from
/// the payload <c>Data</c> dictionary. Multilingual variants remain P2.
/// </summary>
public static class NotificationTemplates
{
    private static readonly Dictionary<string, (string Subject, string Body)> Defaults =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Request.StatusChanged"] = (
                "Your request status has changed",
                "Request {requestId} is now {status}."),
            ["Request.Assigned"] = (
                "A responder has been assigned",
                "Your request {requestId} has been assigned to {assignee}."),
            ["Shelter.Allocated"] = (
                "Shelter allocation confirmed",
                "You have been allocated to {shelter}."),
            ["Donation.Received"] = (
                "Donation received",
                "Thank you. Your donation of {amount} {currency} has been recorded."),
            ["Account.Welcome"] = (
                "Welcome to SAFEWAY",
                "Hello {name}, your account is ready."),
            ["Emergency.Announcement"] = (
                "Emergency announcement",
                "{message}"),
            ["Announcement.Approved"] = (
                "Emergency announcement",
                "{message}"),
            ["Announcement.Rejected"] = (
                "Announcement update",
                "{message}")
        };

    public static (string Subject, string Body) Render(NotificationPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var data = payload.Data ?? new Dictionary<string, string>();

        var hasTemplate = Defaults.TryGetValue(payload.TemplateKey, out var template);

        var subject = !string.IsNullOrWhiteSpace(payload.Subject)
            ? payload.Subject!
            : hasTemplate
                ? template.Subject
                : Humanize(payload.TemplateKey);

        var body = !string.IsNullOrWhiteSpace(payload.Body)
            ? payload.Body!
            : hasTemplate
                ? template.Body
                : DescribeData(data);

        return (Substitute(subject, data), Substitute(body, data));
    }

    private static string Substitute(
        string template,
        IReadOnlyDictionary<string, string> data)
    {
        if (string.IsNullOrEmpty(template) || data.Count == 0)
        {
            return template;
        }

        return Regex.Replace(
            template,
            "\\{([^{}]+)\\}",
            match =>
            {
                var key = match.Groups[1].Value;

                if (data.TryGetValue(key, out var value))
                {
                    return value;
                }

                foreach (var pair in data)
                {
                    if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }

                return match.Value;
            });
    }

    private static string Humanize(string templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return "SAFEWAY notification";
        }

        var lastSegment = templateKey.Split('.').Last();

        var builder = new StringBuilder(lastSegment.Length + 8);

        for (var index = 0; index < lastSegment.Length; index++)
        {
            var character = lastSegment[index];

            if (index > 0 && char.IsUpper(character))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static string DescribeData(IReadOnlyDictionary<string, string> data)
    {
        if (data.Count == 0)
        {
            return "You have a new SAFEWAY notification.";
        }

        return string.Join(
            Environment.NewLine,
            data.Select(pair => $"{pair.Key}: {pair.Value}"));
    }
}
