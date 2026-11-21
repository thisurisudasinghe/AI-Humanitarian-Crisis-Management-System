using System.Net;
using System.Text;
using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cms.Tests;

/// <summary>
/// Failure-injection coverage for the Request.Submitted consumer. The citizen
/// submit path only enqueues the event; triage runs after the transaction.
/// </summary>
public class RequestSubmittedHandlerTests
{
    [Fact]
    public async Task HandleAsync_StoresTriageRecommendations_WithoutChangingRequestStatus()
    {
        using var db = TestDb.Create();
        var request = NewRequest();
        db.Requests.Add(request);
        await db.SaveChangesAsync();

        var handler = new RequestSubmittedHandler(
            db,
            new RuleBasedProvider(),
            new RecommendationService(db, CreateAuditLogger()),
            NullLogger<RequestSubmittedHandler>.Instance);

        await handler.HandleAsync(NewMessage(request));

        var recommendations = await db.Recommendations.ToListAsync();
        Assert.Contains(recommendations, r => r.Type == RecommendationTypes.Classification);
        Assert.Contains(recommendations, r => r.Type == RecommendationTypes.Priority);
        Assert.Contains(recommendations, r => r.Type == RecommendationTypes.Anomaly);
        Assert.All(recommendations, r => Assert.Equal(RecommendationSources.Rule, r.Source));
        Assert.Equal(RequestStatus.Submitted, (await db.Requests.SingleAsync()).Status);
    }

    [Fact]
    public async Task HandleAsync_WhenLlmFails_StoresRuleRecommendations()
    {
        using var db = TestDb.Create();
        var request = NewRequest();
        db.Requests.Add(request);
        await db.SaveChangesAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Llm:Endpoint"] = "https://llm.invalid/analyze"
            })
            .Build();
        var selector = new AiProviderSelector(
            new LlmProvider(
                new SingleClientFactory(new HttpClient(new ThrowingHandler(
                    new HttpRequestException("AI unavailable")))),
                configuration),
            new RuleBasedProvider(),
            NullLogger<AiProviderSelector>.Instance);

        var handler = new RequestSubmittedHandler(
            db,
            selector,
            new RecommendationService(db, CreateAuditLogger()),
            NullLogger<RequestSubmittedHandler>.Instance);

        await handler.HandleAsync(NewMessage(request));

        var recommendations = await db.Recommendations.ToListAsync();
        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r => Assert.Equal(RecommendationSources.Rule, r.Source));
    }

    [Fact]
    public async Task HandleAsync_WhenAiProviderThrows_DoesNotThrowOrBlock()
    {
        using var db = TestDb.Create();
        var request = NewRequest();
        db.Requests.Add(request);
        await db.SaveChangesAsync();

        var ai = new Mock<IAiProvider>();
        ai.Setup(p => p.AnalyzeAsync(
                It.IsAny<Request>(),
                It.IsAny<IReadOnlyList<Request>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("AI unavailable"));
        var recommendations = new Mock<IRecommendationService>();

        var handler = new RequestSubmittedHandler(
            db,
            ai.Object,
            recommendations.Object,
            NullLogger<RequestSubmittedHandler>.Instance);

        await handler.HandleAsync(NewMessage(request));

        recommendations.Verify(
            s => s.CreateAsync(It.IsAny<CreateRecommendationRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(RequestStatus.Submitted, (await db.Requests.SingleAsync()).Status);
    }

    private static IAuditLogger CreateAuditLogger()
    {
        var audit = new Mock<IAuditLogger>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return audit.Object;
    }

    private static Request NewRequest() => new()
    {
        RequesterUserId = Guid.NewGuid(),
        Category = Category.Food,
        Urgency = Urgency.Medium,
        Description = "A trapped family needs food and water",
        Lat = 6.9m,
        Lng = 79.8m,
        HouseholdSize = 4,
        SubmittedAt = DateTime.UtcNow
    };

    private static OutboxMessage NewMessage(Request request) => new()
    {
        Type = OutboxTypes.RequestSubmitted,
        EntityId = request.Id,
        Payload = JsonSerializer.Serialize(new RequestSubmittedPayloadDto { RequestId = request.Id }),
        IdempotencyKey = $"{OutboxTypes.RequestSubmitted}:{request.Id}:1"
    };

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public SingleClientFactory(HttpClient client) => _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }
}
