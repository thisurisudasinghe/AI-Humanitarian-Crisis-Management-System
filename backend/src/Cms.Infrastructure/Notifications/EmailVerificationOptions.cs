namespace Cms.Infrastructure.Notifications;

public sealed class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    /// <summary>Absolute HTTPS URL of the frontend verification route.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Publicly accessible HTTPS URL of the Safeway email logo.</summary>
    public string LogoUrl { get; set; } = string.Empty;

    public int TokenExpirationMinutes { get; set; } = 30;

    public int ResendCooldownSeconds { get; set; } = 60;

    public int RateLimitPermitLimit { get; set; } = 5;
}
