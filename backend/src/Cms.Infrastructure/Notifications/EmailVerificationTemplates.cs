using System.Net;
using System.Reflection;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Renders the Safeway verification emails from the shared template files in
/// docs/email-templates. The files are embedded in this assembly so a deployed
/// API never depends on a repository-relative path.
/// </summary>
public static class EmailVerificationTemplates
{
    public const string RegisterSubject = "Welcome to Safeway – Verify Your Email";
    public const string ResendSubject = "Safeway – Your New Email Verification Link";

    private const string RegisterHtmlResource = "Cms.Infrastructure.EmailTemplates.Register.Html";
    private const string RegisterTextResource = "Cms.Infrastructure.EmailTemplates.Register.Text";
    private const string ResendHtmlResource = "Cms.Infrastructure.EmailTemplates.Resend.Html";
    private const string ResendTextResource = "Cms.Infrastructure.EmailTemplates.Resend.Text";

    public static (string Html, string Text) RenderRegistration(
        string displayName,
        string logoUrl,
        string verificationUrl) =>
        Render(RegisterHtmlResource, RegisterTextResource, displayName, logoUrl, verificationUrl);

    public static (string Html, string Text) RenderResend(
        string displayName,
        string logoUrl,
        string verificationUrl) =>
        Render(ResendHtmlResource, ResendTextResource, displayName, logoUrl, verificationUrl);

    private static (string Html, string Text) Render(
        string htmlResource,
        string textResource,
        string displayName,
        string logoUrl,
        string verificationUrl)
    {
        var safeDisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "there"
            : displayName.Trim();

        var html = ReadResource(htmlResource)
            .Replace("{{LOGO_URL}}", WebUtility.HtmlEncode(logoUrl), StringComparison.Ordinal)
            .Replace("{{FIRST_NAME}}", WebUtility.HtmlEncode(safeDisplayName), StringComparison.Ordinal)
            .Replace("{{VERIFICATION_URL}}", WebUtility.HtmlEncode(verificationUrl), StringComparison.Ordinal);

        var text = ReadResource(textResource)
            .Replace("{{LOGO_URL}}", logoUrl, StringComparison.Ordinal)
            .Replace("{{FIRST_NAME}}", SingleLine(safeDisplayName), StringComparison.Ordinal)
            .Replace("{{VERIFICATION_URL}}", verificationUrl, StringComparison.Ordinal);

        return (html, text);
    }

    private static string ReadResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Safeway email template '{name}' is missing from the application assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string SingleLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
