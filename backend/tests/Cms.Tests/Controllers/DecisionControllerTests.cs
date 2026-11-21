using Cms.Api.Controllers;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Cms.Tests.Controllers;

public class DecisionControllerTests
{
    private readonly Mock<IDecisionService> _decisionServiceMock = new();

    private DecisionController CreateController()
    {
        return new DecisionController(_decisionServiceMock.Object);
    }

    [Fact]
    public async Task RecordDecision_ValidRequest_ReturnsCreated()
    {
        var recommendationId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();

        _decisionServiceMock
            .Setup(x => x.RecordDecisionAsync(
                recommendationId,
                DecisionOutcome.Approved,
                "Approved",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(decisionId);

        var controller = CreateController();

        var request = new DecisionController.RecordDecisionRequest(
            recommendationId,
            DecisionOutcome.Approved,
            "Approved");

        var result = await controller.RecordDecision(
            request,
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);

        Assert.NotNull(createdResult.Value);

        var returnedId = createdResult.Value
            .GetType()
            .GetProperty("decisionId")!
            .GetValue(createdResult.Value);

        Assert.Equal(decisionId, returnedId);
    }

    [Fact]
    public async Task RecordDecision_RecommendationNotFound_ReturnsNotFound()
    {
        _decisionServiceMock
            .Setup(x => x.RecordDecisionAsync(
                It.IsAny<Guid>(),
                It.IsAny<DecisionOutcome>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Recommendation was not found."));

        var controller = CreateController();

        var request = new DecisionController.RecordDecisionRequest(
            Guid.NewGuid(),
            DecisionOutcome.Approved,
            "Approved");

        var result = await controller.RecordDecision(
            request,
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetDecisions_ValidRecommendation_ReturnsOk()
    {
        var recommendationId = Guid.NewGuid();

        var decisions = new List<DecisionRecord>
    {
        new DecisionRecord
        {
            Id = Guid.NewGuid(),
            RecommendationId = recommendationId,
            Outcome = DecisionOutcome.Approved,
            Reason = "Approved",
            DecidedByUserId = Guid.NewGuid(),
            DecidedAt = DateTime.UtcNow
        }
    };

        _decisionServiceMock
            .Setup(x => x.GetDecisionsAsync(
                recommendationId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(decisions);

        var controller = CreateController();

        var result = await controller.GetDecisions(
            recommendationId,
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);

        var returnedDecisions =
            Assert.IsAssignableFrom<IReadOnlyList<DecisionRecord>>(
                okResult.Value);

        Assert.Single(returnedDecisions);
        Assert.Equal(DecisionOutcome.Approved, returnedDecisions[0].Outcome);
    }

    [Fact]
    public async Task GetDecisions_RecommendationNotFound_ReturnsNotFound()
    {
        var recommendationId = Guid.NewGuid();

        _decisionServiceMock
            .Setup(x => x.GetDecisionsAsync(
                recommendationId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new KeyNotFoundException("Recommendation was not found."));

        var controller = CreateController();

        var result = await controller.GetDecisions(
            recommendationId,
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}