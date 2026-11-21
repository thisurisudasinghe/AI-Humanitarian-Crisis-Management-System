using System.Net;
using System.Text;
using Cms.Domain.Constants;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cms.Tests;

/// <summary>
/// Failure-injection coverage for the optional LLM. In every failure mode the
/// rule provider must still answer so request submission is never blocked.
/// </summary>
public class AiProviderSelectorTests
{
    [Fact]
    public async Task HttpFailure_FallsBackToRuleProvider()
    {
        var selector = CreateSelector(new ThrowingHandler(new HttpRequestException("AI down")));

        var result = await selector.AnalyzeAsync(NewRequest(), Array.Empty<Request>());

        Assert.Equal(RecommendationSources.Rule, result.Source);
        Assert.Contains(result.Suggestions, suggestion => suggestion.Type == RecommendationTypes.Classification);
    }

    [Fact]
    public async Task Timeout_FallsBackToRuleProvider()
    {
        var selector = CreateSelector(new ThrowingHandler(new TaskCanceledException("AI timed out")));

        var result = await selector.AnalyzeAsync(NewRequest(), Array.Empty<Request>());

        Assert.Equal(RecommendationSources.Rule, result.Source);
    }

    [Fact]
    public async Task LowConfidence_FallsBackToRuleProvider()
    {
        const string json = """
        {"suggestions":[{"type":"Classification","source":"Ai","confidence":0.2,"rationale":"weak"}]}
        """;
        var selector = CreateSelector(new JsonHandler(json));

        var result = await selector.AnalyzeAsync(NewRequest(), Array.Empty<Request>());

        Assert.Equal(RecommendationSources.Rule, result.Source);
    }

    [Fact]
    public async Task ConfidentLlmResult_IsUsed()
    {
        const string json = """
        {"suggestions":[{"type":"Priority","source":"Ai","confidence":0.9,"rationale":"clear urgency signals"}]}
        """;
        var selector = CreateSelector(new JsonHandler(json));

        var result = await selector.AnalyzeAsync(NewRequest(), Array.Empty<Request>());

        Assert.Equal(RecommendationSources.Ai, result.Source);
        Assert.Single(result.Suggestions);
        Assert.Equal(0.9m, result.Suggestions[0].Confidence);
    }

    private static AiProviderSelector CreateSelector(HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Llm:Endpoint"] = "https://llm.invalid/analyze"
            })
            .Build();

        var factory = new SingleClientFactory(new HttpClient(handler));
        var llm = new LlmProvider(factory, configuration);
        return new AiProviderSelector(
            llm,
            new RuleBasedProvider(),
            NullLogger<AiProviderSelector>.Instance);
    }

    private static Request NewRequest() => new()
    {
        Id = Guid.NewGuid(),
        Category = Category.Medicine,
        Urgency = Urgency.High,
        Description = "Medical help needed urgently"
    };

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
