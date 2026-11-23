using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cms.Tests.Integration;

public sealed class SecurityBaselineTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public SecurityBaselineTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Security_headers_are_present_on_public_and_protected_responses()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var publicResponse = await client.GetAsync("/api/Health");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        await AssertSecurityHeadersAsync(publicResponse);

        var protectedResponse = await client.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        await AssertSecurityHeadersAsync(protectedResponse);
    }

    [Fact]
    public async Task Informational_chat_is_available_to_guests_but_decision_routes_are_not()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var chatResponse = await client.PostAsJsonAsync("/api/v1/ai/chat", new
        {
            message = "How do I report an emergency?"
        });

        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);

        using var document = JsonDocument.Parse(await chatResponse.Content.ReadAsStringAsync());
        var answer = document.RootElement.GetProperty("answer").GetString();

        Assert.Contains("local emergency services", answer ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.True(document.RootElement.GetProperty("decidesNothing").GetBoolean());

        var decisionResponse = await client.GetAsync("/api/v1/recommendations");
        Assert.Equal(HttpStatusCode.Unauthorized, decisionResponse.StatusCode);
    }

    [Fact]
    public async Task Validation_failure_returns_422_with_stable_code()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "not-an-email",
            password = ""
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").EnumerateObject().Any());
        await AssertSecurityHeadersAsync(response);
    }

    [Fact]
    public async Task Oversized_body_is_rejected_with_413()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = CmsWebApplicationFactory.CitizenEmail,
            password = new string('x', 1_200_000)
        });

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("REQUEST_TOO_LARGE", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Audit_event_for_failed_login_masks_the_email()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = CmsWebApplicationFactory.CitizenEmail,
            password = "WrongPassword1!"
        });

        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        var response = await admin.GetAsync("/api/v1/audit-events?entityType=identity.User&page=1&pageSize=50");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var serialized = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(CmsWebApplicationFactory.CitizenEmail, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", serialized, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _ = document;
    }

    private static async Task AssertSecurityHeadersAsync(HttpResponseMessage response)
    {
        Assert.Equal("nosniff", GetHeader(response, "X-Content-Type-Options"));
        Assert.Equal("DENY", GetHeader(response, "X-Frame-Options"));
        Assert.Equal("no-referrer", GetHeader(response, "Referrer-Policy"));
        Assert.Contains("frame-ancestors 'none'", GetHeader(response, "Content-Security-Policy"));
        await Task.CompletedTask;
    }

    private static string? GetHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : response.Content.Headers.TryGetValues(name, out var contentValues)
                ? contentValues.FirstOrDefault()
                : null;
}
