namespace Cms.Infrastructure.Notifications;

/// <summary>SMTP configuration (section <c>Smtp</c> in appsettings).</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@safeway.local";

    public string FromName { get; set; } = "SAFEWAY";

    public int TimeoutSeconds { get; set; } = 15;
}
