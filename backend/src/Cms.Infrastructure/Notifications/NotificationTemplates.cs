using System.Text;
using System.Text.RegularExpressions;
using Cms.Application.DTOs.Notifications;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Template registry for the frozen Notification.Send payload. Templates are
/// keyed by TemplateKey and the P2 languages seeded in common.notification_templates
/// (English and Spanish). A payload may override Subject/Body directly; otherwise
/// the registry substitutes {key} tokens from the payload Data dictionary.
/// </summary>
public static class NotificationTemplates
{
    public static readonly string[] SupportedLanguages = { "en", "es" };

    private static readonly Dictionary<(string Key, string Language), (string Subject, string Body)> Defaults =
        new()
        {
            [("Request.StatusChanged", "en")] =
                ("Your request status has changed", "Request {requestId} is now {status}."),
            [("Request.StatusChanged", "es")] =
                ("El estado de su solicitud ha cambiado", "La solicitud {requestId} ahora está {status}."),
            [("Request.Assigned", "en")] =
                ("A responder has been assigned", "Your request {requestId} has been assigned to {assignee}."),
            [("Request.Assigned", "es")] =
                ("Se ha asignado un responder", "Su solicitud {requestId} ha sido asignada a {assignee}."),
            [("Shelter.Allocated", "en")] =
                ("Shelter allocation confirmed", "You have been allocated to {shelter}."),
            [("Shelter.Allocated", "es")] =
                ("Asignación de refugio confirmada", "Ha sido asignado a {shelter}."),
            [("Donation.Received", "en")] =
                ("Donation received", "Thank you. Your donation of {amount} {currency} has been recorded."),
            [("Donation.Received", "es")] =
                ("Donación recibida", "Gracias. Su donación de {amount} {currency} ha sido registrada."),
            [("Account.Welcome", "en")] =
                ("Welcome to SAFEWAY", "Hello {name}, your account is ready."),
            [("Account.Welcome", "es")] =
                ("Bienvenido a SAFEWAY", "Hola {name}, su cuenta está lista."),
            [("Emergency.Announcement", "en")] =
                ("Emergency announcement", "{message}"),
            [("Emergency.Announcement", "es")] =
                ("Anuncio de emergencia", "{message}"),
            [("Announcement.Approved", "en")] =
                ("Emergency announcement", "{message}"),
            [("Announcement.Approved", "es")] =
                ("Anuncio de emergencia", "{message}"),
            [("Announcement.Rejected", "en")] =
                ("Announcement update", "{message}"),
            [("Announcement.Rejected", "es")] =
                ("Actualización del anuncio", "{message}")
        };

    public static (string Subject, string Body) Render(NotificationPayloadDto payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var data = payload.Data ?? new Dictionary<string, string>();
        var language = NormaliseLanguage(payload.Language);
        var key = payload.TemplateKey ?? string.Empty;

        Defaults.TryGetValue((key, language), out var template);
        if (template == default && language != "en")
        {
            Defaults.TryGetValue((key, "en"), out template);
        }

        var hasTemplate = template != default;

        var subject = !string.IsNullOrWhiteSpace(payload.Subject)
            ? payload.Subject!
            : hasTemplate
                ? template.Subject
                : Humanize(key);

        var body = !string.IsNullOrWhiteSpace(payload.Body)
            ? payload.Body!
            : hasTemplate
                ? template.Body
                : DescribeData(data);

        return (Substitute(subject, data), Substitute(body, data));
    }

    private static string NormaliseLanguage(string? language)
    {
        var normalised = language?.Trim().ToLowerInvariant();
        return normalised is "en" or "es" ? normalised : "en";
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
