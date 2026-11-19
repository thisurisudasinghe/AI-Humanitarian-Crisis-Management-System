using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Cms.Infrastructure.Http;

/// <summary>
/// The only HTTP client factory for external services (geocoding, routing, email, AI).
/// Every outbound call must use this client so timeouts, retries, circuit breaking
/// and the identifying User-Agent are applied consistently.
/// </summary>
public static class HttpClientExtensions
{
    public const string CmsExternalClientName = "cms-external";

    public static IServiceCollection AddCmsHttpClient(
        this IServiceCollection services,
        string name = CmsExternalClientName,
        string? baseUrl = null)
    {
        services
            .AddHttpClient(name, client =>
            {
                if (!string.IsNullOrWhiteSpace(baseUrl) &&
                    Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }

                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "CmsPlatform/1.0 (AI Humanitarian Crisis Management; contact: cms-team@example.org)");
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.Delay = TimeSpan.FromMilliseconds(200);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;

                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);

                // Sampling duration must be at least twice the attempt timeout.
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                options.CircuitBreaker.FailureRatio = 0.5;
                options.CircuitBreaker.MinimumThroughput = 5;
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            });

        return services;
    }
}
