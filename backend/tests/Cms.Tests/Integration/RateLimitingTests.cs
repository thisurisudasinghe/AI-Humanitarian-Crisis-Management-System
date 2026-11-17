using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Cms.Tests.Integration;

public sealed class RateLimitingTests
{
    [Fact]
    public async Task Auth_policy_returns_429_with_retry_after()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        using var client = factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 61; attempt++)
        {
            last?.Dispose();
            last = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = CmsWebApplicationFactory.CitizenEmail,
                password = "WrongPassword1!"
            });

            if (last.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.Equal("60", Assert.Single(retryAfter));

        last.Dispose();
    }

    [Fact]
    public void Non_development_startup_fails_when_secrets_are_missing()
    {
        using var factory = new MissingSecretsFactory();

        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Missing required configuration", exception!.ToString(), StringComparison.Ordinal);
    }

    private sealed class MissingSecretsFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=unused",
                    ["Jwt:SecretKey"] = null,
                    ["EmailProtection:EncryptionKey"] = null,
                    ["EmailProtection:HashKey"] = null
                });
            });
        }
    }
}
