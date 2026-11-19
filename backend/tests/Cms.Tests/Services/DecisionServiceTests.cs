
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Cms.Tests.Services;

public class DecisionServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IAuditService> _auditMock = new();
    private readonly Mock<IDateTimeService> _dateTimeMock = new();

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task RecordDecisionAsync_AuthenticatedUser_SavesDecision()
    {
        // Arrange
        await using var context = CreateContext();

        var userId = Guid.NewGuid();
        var recommendation = new Recommendation
        {
            Type = "CrisisResponse",
            TargetId = Guid.NewGuid(),
            PayloadJson = "{}",
            Confidence = 0.70m,
            Rationale = "Test recommendation",
            Source = "RuleBased"
        };

        await context.Recommendations.AddAsync(recommendation);
        await context.SaveChangesAsync();

        var expectedDate = DateTime.UtcNow;

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(userId);
        _dateTimeMock.Setup(x => x.UtcNow).Returns(expectedDate);

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        // Act
        var decisionId = await service.RecordDecisionAsync(
            recommendation.Id,
            DecisionOutcome.Approve,
            "Approved for implementation");

        // Assert
        var savedDecision = await context.DecisionRecords
            .FirstOrDefaultAsync(x => x.Id == decisionId);

        Assert.NotNull(savedDecision);
        Assert.Equal(DecisionOutcome.Approve, savedDecision.Outcome);
        Assert.Equal(userId, savedDecision.DecidedByUserId);
        Assert.Equal(expectedDate, savedDecision.DecidedAt);

        _auditMock.Verify(x => x.LogAsync(
            userId,
            "Recommendation.Approve",
            nameof(Recommendation),
            recommendation.Id.ToString(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordDecisionAsync_UnauthenticatedUser_ThrowsException()
    {
        // Arrange
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(false);
        _currentUserMock.Setup(x => x.UserId).Returns((Guid?)null);

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                DecisionOutcome.Approve,
                "Test reason"));
    }

    [Fact]
    public async Task RecordDecisionAsync_EmptyReason_ThrowsException()
    {
        // Arrange
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                DecisionOutcome.Approve,
                ""));
    }

    [Fact]
    public async Task RecordDecisionAsync_ModifyWithoutPayload_ThrowsException()
    {
        // Arrange
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                DecisionOutcome.Modify,
                "Modified recommendation",
                null));
    }

    [Fact]
    public async Task RecordDecisionAsync_RecommendationNotFound_ThrowsException()
    {
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                DecisionOutcome.Approve,
                "Test reason"));
    }

    [Fact]
    public async Task GetDecisionsAsync_ExistingRecommendation_ReturnsDecisions()
    {
        await using var context = CreateContext();

        var recommendation = new Recommendation
        {
            Type = "CrisisResponse",
            TargetId = Guid.NewGuid(),
            PayloadJson = "{}",
            Confidence = 0.85m,
            Rationale = "Test recommendation",
            Source = "RuleBased"
        };

        await context.Recommendations.AddAsync(recommendation);

        var decision = new DecisionRecord
        {
            RecommendationId = recommendation.Id,
            Outcome = DecisionOutcome.Approve,
            Reason = "Approved",
            DecidedByUserId = Guid.NewGuid(),
            DecidedAt = DateTime.UtcNow
        };

        await context.DecisionRecords.AddAsync(decision);
        await context.SaveChangesAsync();

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        var result = await service.GetDecisionsAsync(recommendation.Id);

        Assert.Single(result);
        Assert.Equal(DecisionOutcome.Approve, result[0].Outcome);
        Assert.Equal("Approved", result[0].Reason);
    }

    [Fact]
    public async Task RecordDecisionAsync_ModifyWithPayload_SavesDecision()
    {
        await using var context = CreateContext();

        var recommendation = new Recommendation
        {
            Type = "CrisisResponse",
            TargetId = Guid.NewGuid(),
            PayloadJson = "{}",
            Confidence = 0.85m,
            Rationale = "Test recommendation",
            Source = "RuleBased"
        };

        await context.Recommendations.AddAsync(recommendation);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        var payload = "{\"action\":\"Updated response\"}";

        var decisionId = await service.RecordDecisionAsync(
            recommendation.Id,
            DecisionOutcome.Modify,
            "Updated recommendation",
            payload);

        var savedDecision = await context.DecisionRecords
            .FirstOrDefaultAsync(x => x.Id == decisionId);

        Assert.NotNull(savedDecision);
        Assert.Equal(DecisionOutcome.Modify, savedDecision.Outcome);
        Assert.Equal(payload, savedDecision.FinalPayloadJson);
    }

    [Theory]
    [InlineData(DecisionOutcome.Reject)]
    [InlineData(DecisionOutcome.Defer)]
    public async Task RecordDecisionAsync_RejectOrDefer_SavesDecision(
    DecisionOutcome outcome)
    {
        await using var context = CreateContext();

        var recommendation = new Recommendation
        {
            Type = "CrisisResponse",
            TargetId = Guid.NewGuid(),
            PayloadJson = "{}",
            Confidence = 0.85m,
            Rationale = "Test recommendation",
            Source = "RuleBased"
        };

        await context.Recommendations.AddAsync(recommendation);
        await context.SaveChangesAsync();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        var decisionId = await service.RecordDecisionAsync(
            recommendation.Id,
            outcome,
            "Test decision");

        var savedDecision = await context.DecisionRecords
            .FirstOrDefaultAsync(x => x.Id == decisionId);

        Assert.NotNull(savedDecision);
        Assert.Equal(outcome, savedDecision.Outcome);
    }

    [Fact]
    public async Task RecordDecisionAsync_ReasonTooLong_ThrowsException()
    {
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        var longReason = new string('A', 2001);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                DecisionOutcome.Approve,
                longReason));
    }

    [Fact]
    public async Task RecordDecisionAsync_InvalidOutcome_ThrowsException()
    {
        await using var context = CreateContext();

        _currentUserMock.Setup(x => x.IsAuthenticated).Returns(true);
        _currentUserMock.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var service = new DecisionService(
            context,
            _currentUserMock.Object,
            _auditMock.Object,
            _dateTimeMock.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.RecordDecisionAsync(
                Guid.NewGuid(),
                (DecisionOutcome)999,
                "Invalid outcome"));
    }
}
