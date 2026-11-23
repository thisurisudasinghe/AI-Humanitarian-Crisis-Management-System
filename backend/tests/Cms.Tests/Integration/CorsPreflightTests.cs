using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Cms.Tests.Integration;

public sealed class CorsPreflightTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public CorsPreflightTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:3001")]
    public async Task Preflight_from_allowed_development_origin_returns_cors_headers(string origin)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin));
        Assert.Contains(origin, allowOrigin);
    }

    [Fact]
    public async Task Preflight_from_disallowed_origin_does_not_return_cors_allow_origin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
