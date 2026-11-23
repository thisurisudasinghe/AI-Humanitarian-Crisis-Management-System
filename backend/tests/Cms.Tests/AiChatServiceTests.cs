using System.Net;
using System.Text;
using Cms.Application.DTOs.AI;
using Cms.Domain.Constants;
using Cms.Infrastructure.Services.AI;
using Microsoft.Extensions.Configuration;

namespace Cms.Tests;

public class AiChatServiceTests
{
    [Fact]
    public async Task FixedFaq_AnswersOffline_AndDecidesNothing()
    {
        var service = CreateService(null);

        var result = await service.AnswerAsync(new AiChatRequestDto
        {
            Message = "Who makes the final decision?"
        });

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.True(result.DecidesNothing);
        Assert.Contains("does not submit, approve, reject or change any request", result.Answer);
        Assert.Contains("authorised human", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmFailure_FallsBackToFixedFaq()
    {
        var service = CreateService(new ThrowingHandler(new HttpRequestException("chat down")));

        var result = await service.AnswerAsync(new AiChatRequestDto
        {
            Message = "How do I submit a request?"
        });

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.True(result.DecidesNothing);
        Assert.Contains("Use Report to submit a request", result.Answer, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("How do I report an emergency?", "local emergency services")]
    [InlineData("How do I find a shelter?", "Shelters page")]
    [InlineData("What resources are available?", "Resources page")]
    [InlineData("How do I volunteer?", "Volunteers page")]
    [InlineData("How can I donate?", "Donations page")]
    [InlineData("How does the workflow work?", "Submitted")]
    [InlineData("How do I use SAFEWAY?", "SAFEWAY helps")]
    public async Task GlobalFaq_CoversAssistantTopicsOffline(string question, string expected)
    {
        var service = CreateService(null);

        var result = await service.AnswerAsync(new AiChatRequestDto { Message = question });

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.Contains(expected, result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not provide live shelter availability", result.Answer);
    }

    [Fact]
    public async Task MissingAiProvider_ReturnsClearConfigurationNotice()
    {
        var service = CreateService(null);

        var result = await service.AnswerAsync(new AiChatRequestDto
        {
            Message = "How can I donate?"
        });

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.Contains("not configured", result.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verified FAQ", result.Notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LlmAnswer_StillDecidesNothing()
    {
        const string json = """
        {"answer":"Here is general guidance.","suggestions":["How do I check status?"]}
        """;
        var service = CreateService(new JsonHandler(json));

        var result = await service.AnswerAsync(new AiChatRequestDto
        {
            Message = "What can you tell me?"
        });

        Assert.Equal(RecommendationSources.Ai, result.Source);
        Assert.True(result.DecidesNothing);
        Assert.Equal(string.Empty, result.Notice);
        Assert.Contains("does not submit, approve, reject or change any request", result.Answer);
    }

    private static AiChatService CreateService(HttpMessageHandler? handler)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cases:DefaultLanguage"] = "en"
        };
        if (handler != null)
        {
            settings["Ai:Chat:Endpoint"] = "https://chat.invalid/answer";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var client = handler == null
            ? new HttpClient(new ThrowingHandler(new InvalidOperationException("not configured")))
            : new HttpClient(handler);

        return new AiChatService(new SingleClientFactory(client), configuration);
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class JsonHandler : HttpMessageHandler
    {
        private readonly string _json;

        public JsonHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(_exception);
    }
}
