namespace Cms.Infrastructure.Notifications;

public sealed class ResendOptions
{
    public const string SectionName = "Resend";

    public string ApiKey { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Safeway";

    public string ApiBaseUrl { get; set; } = "https://api.resend.com/";
}
