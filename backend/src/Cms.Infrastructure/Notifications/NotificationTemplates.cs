using System.Text;
using System.Text.RegularExpressions;
using Cms.Application.DTOs.Notifications;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Template registry for the frozen Notification.Send payload. Templates are
/// keyed by TemplateKey and language. The registry supplies English, Sinhala,
/// Tamil and Spanish defaults for the core notification keys. A payload may
/// override Subject/Body directly; otherwise the registry substitutes {key}
/// tokens from the payload Data dictionary. User-generated text is passed
/// through unchanged.
/// </summary>
public static class NotificationTemplates
{
    public static readonly string[] SupportedLanguages = { "en", "si", "ta", "es" };

    private static readonly Dictionary<(string Key, string Language), (string Subject, string Body)> Defaults =
        new()
        {
            [("Request.StatusChanged", "en")] =
                ("Your request status has changed", "Request {requestId} is now {status}."),
            [("Request.StatusChanged", "si")] =
                ("ඔබේ ඉල්ලීමේ තත්ත්වය යාවත්කාලීනයි", "{requestId} ඉල්ලීම දැන් {status} තත්ත්වයේ පවතී."),
            [("Request.StatusChanged", "ta")] =
                ("உங்கள் கோரிக்கையின் நிலை மாற்றம்", "கோரிக்கை {requestId} இப்போது {status} நிலையில் உள்ளது."),
            [("Request.StatusChanged", "es")] =
                ("El estado de su solicitud ha cambiado", "La solicitud {requestId} ahora está {status}."),
            [("Request.Assigned", "en")] =
                ("A responder has been assigned", "Your request {requestId} has been assigned to {assignee}."),
            [("Request.Assigned", "si")] =
                ("ප්‍රතිචාරකයෙකු පවරා ඇත", "ඔබේ {requestId} ඉල්ලීම {assignee} වෙත පවරා ඇත."),
            [("Request.Assigned", "ta")] =
                ("பதிலளிப்பவர் நியமிக்கப்பட்டுள்ளார்", "உங்கள் கோரிக்கை {requestId} {assignee} க்கு நியமிக்கப்பட்டுள்ளது."),
            [("Request.Assigned", "es")] =
                ("Se ha asignado un responder", "Su solicitud {requestId} ha sido asignada a {assignee}."),
            [("Shelter.Allocated", "en")] =
                ("Shelter allocation confirmed", "You have been allocated to {shelter}."),
            [("Shelter.Allocated", "si")] =
                ("නවාතැන් වෙන් කිරීම තහවුරුයි", "ඔබට {shelter} නවාතැන වෙන් කර ඇත."),
            [("Shelter.Allocated", "ta")] =
                ("இல்லம் ஒதுக்கீடு உறுதிப்படுத்தப்பட்டது", "உங்களுக்கு {shelter} இல்லம் ஒதுக்கப்பட்டுள்ளது."),
            [("Shelter.Allocated", "es")] =
                ("Asignación de refugio confirmada", "Ha sido asignado a {shelter}."),
            [("Donation.Received", "en")] =
                ("Donation received", "Thank you. Donation {reference} for {amount} {currency} has been recorded."),
            [("Donation.Received", "si")] =
                ("පරිත්‍යාගය ලැබුණි", "ස්තූතියි. {reference} පරිත්‍යාගය {amount} {currency} ලෙස සටහන් කර ඇත."),
            [("Donation.Received", "ta")] =
                ("நன்கொடை பெறப்பட்டது", "நன்றி. {reference} நன்கொடை {amount} {currency} ஆகப் பதிவு செய்யப்பட்டுள்ளது."),
            [("Donation.Received", "es")] =
                ("Donación recibida", "Gracias. La donación {reference} por {amount} {currency} ha sido registrada."),
            [("Donation.Allocated", "en")] =
                ("Donation allocated", "Donation {reference} has been allocated to {campaign} ({amount} {currency})."),
            [("Donation.Allocated", "si")] =
                ("පරිත්‍යාගය වෙන් කර ඇත", "{reference} පරිත්‍යාගය {campaign} වෙත වෙන් කර ඇත ({amount} {currency})."),
            [("Donation.Allocated", "ta")] =
                ("நன்கொடை ஒதுக்கப்பட்டது", "{reference} நன்கொடை {campaign} க்கு ஒதுக்கப்பட்டுள்ளது ({amount} {currency})."),
            [("Donation.Allocated", "es")] =
                ("Donación asignada", "La donación {reference} ha sido asignada a {campaign} ({amount} {currency})."),
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
            [("Announcement.Approved", "si")] =
                ("හදිසි නිවේදනය", "{message}"),
            [("Announcement.Approved", "ta")] =
                ("அவசர அறிவிப்பு", "{message}"),
            [("Announcement.Approved", "es")] =
                ("Anuncio de emergencia", "{message}"),
            [("Announcement.Rejected", "en")] =
                ("Announcement update", "{message}"),
            [("Announcement.Rejected", "si")] =
                ("නිවේදන යාවත්කාලීනය", "{message}"),
            [("Announcement.Rejected", "ta")] =
                ("அறிவிப்பு புதுப்பிப்பு", "{message}"),
            [("Announcement.Rejected", "es")] =
                ("Actualización del anuncio", "{message}"),
            [("StaffAccess.ApplicationApproved", "en")] =
                ("Staff access approved", "Application {reference} for {role} was approved. Sign in again to see your new workspace access."),
            [("StaffAccess.ApplicationApproved", "si")] =
                ("කාර්ය මණ්ඩල ප්‍රවේශය අනුමතයි", "{role} සඳහා වූ {reference} අයදුම්පත අනුමත විය. නව ප්‍රවේශය බැලීමට නැවත පුරනය වන්න."),
            [("StaffAccess.ApplicationApproved", "ta")] =
                ("ஊழியர் அணுகல் அங்கீகரிக்கப்பட்டது", "{role} க்கான {reference} விண்ணப்பம் அங்கீகரிக்கப்பட்டது. புதிய அணுகலைக் காண மீண்டும் உள்நுழையவும்."),
            [("StaffAccess.ApplicationApproved", "es")] =
                ("Acceso de personal aprobado", "La solicitud {reference} para {role} fue aprobada. Inicie sesión de nuevo para ver el acceso."),
            [("StaffAccess.ApplicationRejected", "en")] =
                ("Staff access update", "Application {reference} for {role} was not approved. Review the reason in your application status."),
            [("StaffAccess.ApplicationRejected", "si")] =
                ("කාර්ය මණ්ඩල ප්‍රවේශය යාවත්කාලීනය", "{role} සඳහා වූ {reference} අයදුම්පත අනුමත නොවීය. හේතුව අයදුම්පත් තත්ත්වයේ බලන්න."),
            [("StaffAccess.ApplicationRejected", "ta")] =
                ("ஊழியர் அணுகல் புதுப்பிப்பு", "{role} க்கான {reference} விண்ணப்பம் அங்கீகரிக்கப்படவில்லை. காரணத்தை விண்ணப்ப நிலையில் காண்க."),
            [("StaffAccess.ApplicationRejected", "es")] =
                ("Actualización de acceso de personal", "La solicitud {reference} para {role} no fue aprobada. Revise el motivo en el estado de la solicitud.")
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
        return normalised is "en" or "si" or "ta" or "es" ? normalised : "en";
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
