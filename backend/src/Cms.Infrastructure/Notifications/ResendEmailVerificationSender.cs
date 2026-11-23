using System.Net.Http.Json;
using System.Text.Json;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cms.Infrastructure.Notifications;

/// <summary>Sends verification email through the Resend HTTP API.</summary>
public sealed class ResendEmailVerificationSender : IEmailVerificationEmailSender
{
    public const string HttpClientName = "resend";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailVerificationSender> _logger;

    public ResendEmailVerificationSender(
        IHttpClientFactory httpClientFactory,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailVerificationSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        string recipientEmail,
        string displayName,
        string fromName,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "RESEND_API_KEY is not configured. Verification email cannot be sent.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException(
                "RESEND_FROM_EMAIL is not configured. Verification email cannot be sent.");
        }

        if (!Uri.TryCreate(_options.ApiBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Resend API base URL must be an absolute HTTPS URL.");
        }

        var senderName = string.IsNullOrWhiteSpace(fromName) ? "Safeway" : fromName.Trim();
        var payload = new
        {
            from = $"{senderName} <{_options.FromEmail}>",
            to = new[] { recipientEmail },
            subject,
            html = htmlBody,
            text = textBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "emails"))
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        using var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var providerError = await ReadProviderErrorAsync(response, cancellationToken);

            // Never log the request body: it contains the one-time link.
            _logger.LogWarning(
                "Resend rejected verification email (status {Status}, provider error {ProviderError}).",
                (int)response.StatusCode,
                providerError);

            throw new EmailDeliveryException();
        }
    }

    private static async Task<string> ReadProviderErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("name", out var name))
            {
                return name.GetString() ?? "unknown";
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException)
        {
            // A non-JSON provider response is still a delivery failure.
        }

        return "unknown";
    }
}
