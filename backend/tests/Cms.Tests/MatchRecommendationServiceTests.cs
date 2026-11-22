using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using OperationsContracts = Cms.Application.Contracts.Operations;
using Moq;

namespace Cms.Tests;

public class MatchRecommendationServiceTests
{
    [Fact]
    public async Task CreateAsync_StoresTopAndAlternatives_AndAdvancesTriagedRequest()
    {
        using var context = TestDb.Create();
        var request = new Request
        {
            Id = Guid.NewGuid(),
            RequesterUserId = Guid.NewGuid(),
            Category = Category.Food,
            Urgency = Urgency.High,
            Status = RequestStatus.Triaged,
            SubmittedAt = DateTime.UtcNow
        };
        context.Requests.Add(request);
        await context.SaveChangesAsync();

        var resourceMatcher = new Mock<OperationsContracts.IResourceMatcher>();
        resourceMatcher
            .Setup(m => m.RankAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OperationsContracts.MatchCandidate(Guid.NewGuid(), 0.9m, "Closest available stock", false),
                new OperationsContracts.MatchCandidate(Guid.NewGuid(), 0.7m, "Second-best stock", true)
            });

        var requestStatus = new Mock<IRequestStatusService>();
        requestStatus
            .Setup(s => s.TransitionAsync(
                request.Id,
                RequestStatus.Recommended,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new MatchRecommendationService(
            context,
            resourceMatcher.Object,
            Mock.Of<OperationsContracts.IShelterRecommender>(),
            Mock.Of<OperationsContracts.IVolunteerMatcher>(),
            new RecommendationService(context, Mock.Of<IAuditLogger>()),
            requestStatus.Object,
            new DateTimeService());

        var result = await service.CreateAsync(new MatchRecommendationRequestDto
        {
            RequestId = request.Id,
            Type = RecommendationTypes.ResourceMatch
        });

        var stored = await context.Recommendations.SingleAsync();
        using var payload = JsonDocument.Parse(stored.PayloadJson);
        Assert.Equal(RecommendationTypes.ResourceMatch, stored.Type);
        Assert.Equal(RecommendationStatuses.Pending, stored.Status);
        Assert.True(payload.RootElement.GetProperty("top").GetProperty("score").GetDouble() >= 0.9);
        Assert.Single(payload.RootElement.GetProperty("alternatives").EnumerateArray());
        Assert.Equal(RequestStatus.Recommended.ToString(), result.RequestStatus);

        requestStatus.Verify(
            s => s.TransitionAsync(
                request.Id,
                RequestStatus.Recommended,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
