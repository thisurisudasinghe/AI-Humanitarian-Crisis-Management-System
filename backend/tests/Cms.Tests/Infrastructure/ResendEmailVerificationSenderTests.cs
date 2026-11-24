using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cms.Application.Common;
using Cms.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cms.Tests.Infrastructure;

public sealed class ResendEmailVerificationSenderTests
{
    [Fact]
    public async Task SendAsync_posts_branded_payload_with_bearer_key()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var sender = CreateSender(handler, "re_test_key");

        await sender.SendAsync(
            "citizen@example.org",
            "Test Citizen",
            "Safeway",
            EmailVerificationTemplates.RegisterSubject,
            "<html>Welcome to Safeway</html>",
            "Welcome to Safeway");

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.resend.com/emails", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer re_test_key", handler.Request.Headers.GetValues("Authorization").Single());

        using var document = JsonDocument.Parse(handler.Body!);
        var root = document.RootElement;
        Assert.Equal("Safeway <no-reply@safeway.example>", root.GetProperty("from").GetString());
        Assert.Equal("citizen@example.org", root.GetProperty("to")[0].GetString());
        Assert.Equal(EmailVerificationTemplates.RegisterSubject, root.GetProperty("subject").GetString());
        Assert.Contains("Safeway", root.GetProperty("html").GetString());
        Assert.Contains("Safeway", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendAsync_throws_generic_exception_on_provider_rejection()
    {
        var handler = new RecordingHandler(
            HttpStatusCode.BadRequest,
            "{\"name\":\"invalid_api_key\",\"message\":\"secret detail\"}");
        var sender = CreateSender(handler, "re_test_key");

        await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(
            "citizen@example.org",
            "Test Citizen",
            "Safeway",
            "subject",
            "<html>body</html>",
            "body"));
    }

    [Fact]
    public async Task SendAsync_fails_closed_when_api_key_is_missing()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var sender = CreateSender(handler, string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            "citizen@example.org",
            "Test Citizen",
            "Safeway",
            "subject",
            "<html>body</html>",
            "body"));

        Assert.Null(handler.Request);
    }

    private static ResendEmailVerificationSender CreateSender(
        RecordingHandler handler,
        string apiKey)
    {
        var options = Options.Create(new ResendOptions
        {
            ApiKey = apiKey,
            FromEmail = "no-reply@safeway.example",
            FromName = "Safeway",
            ApiBaseUrl = "https://api.resend.com/"
        });

        var factory = new StubHttpClientFactory(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com/")
        });

        return new ResendEmailVerificationSender(
            factory,
            options,
            NullLogger<ResendEmailVerificationSender>.Instance);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = JsonContent.Create(new { name = _responseBody })
            };
        }
    }
}
