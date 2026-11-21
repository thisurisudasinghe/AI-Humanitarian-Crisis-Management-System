using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Services.Governance;
using Moq;

namespace Cms.Tests;

public class DecisionGateTests
{
    private static readonly Guid RequestId = Guid.NewGuid();
    private static readonly Guid RecommendationId = Guid.NewGuid();

    [Fact]
    public async Task RequireApprovedAsync_WithApprovedDecision_ReturnsDecision()
    {
        var decision = MakeDecision(DecisionOutcome.Approved);
        using var context = TestDb.Create();
        context.DecisionRecords.Add(decision);
        await context.SaveChangesAsync();

        var gate = new DecisionGate(context, RecommendationServiceMock());

        var result = await gate.RequireApprovedAsync(
            decision.Id,
            RecommendationTypes.ResourceMatch,
            RequestId);

        Assert.Equal(decision.Id, result.Id);
        Assert.Equal(DecisionOutcome.Approved, result.Outcome);
    }

    [Fact]
    public async Task RequireApprovedAsync_WithModifiedDecision_IsAccepted()
    {
        var decision = MakeDecision(DecisionOutcome.Modified);
        using var context = TestDb.Create();
        context.DecisionRecords.Add(decision);
        await context.SaveChangesAsync();

        var gate = new DecisionGate(context, RecommendationServiceMock());

        var result = await gate.RequireApprovedAsync(
            decision.Id,
            RecommendationTypes.ResourceMatch,
            RequestId);

        Assert.Equal(DecisionOutcome.Modified, result.Outcome);
    }

    [Theory]
    [InlineData(DecisionOutcome.Rejected)]
    [InlineData(DecisionOutcome.Deferred)]
    public async Task RequireApprovedAsync_WithNonApprovalOutcome_ThrowsForbidden(
        DecisionOutcome outcome)
    {
        var decision = MakeDecision(outcome);
        using var context = TestDb.Create();
        context.DecisionRecords.Add(decision);
        await context.SaveChangesAsync();

        var gate = new DecisionGate(context, RecommendationServiceMock());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            gate.RequireApprovedAsync(
                decision.Id,
                RecommendationTypes.ResourceMatch,
                RequestId));
    }

    [Fact]
    public async Task RequireApprovedAsync_WithWrongTypeOrTarget_ThrowsForbidden()
    {
        var decision = MakeDecision(DecisionOutcome.Approved);
        using var context = TestDb.Create();
        context.DecisionRecords.Add(decision);
        await context.SaveChangesAsync();

        var gate = new DecisionGate(context, RecommendationServiceMock());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            gate.RequireApprovedAsync(
                decision.Id,
                RecommendationTypes.ShelterMatch,
                RequestId));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            gate.RequireApprovedAsync(
                decision.Id,
                RecommendationTypes.ResourceMatch,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task RequireApprovedAsync_WithUnknownDecision_ThrowsForbidden()
    {
        using var context = TestDb.Create();
        var gate = new DecisionGate(context, RecommendationServiceMock());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            gate.RequireApprovedAsync(
                Guid.NewGuid(),
                RecommendationTypes.ResourceMatch,
                RequestId));
    }

    private static DecisionRecord MakeDecision(DecisionOutcome outcome) => new()
    {
        RecommendationId = RecommendationId,
        Outcome = outcome,
        Reason = "test",
        DecidedByUserId = Guid.NewGuid(),
        DecidedAt = DateTime.UtcNow
    };

    private static IRecommendationService RecommendationServiceMock()
    {
        var mock = new Mock<IRecommendationService>();
        mock.Setup(s => s.GetByIdAsync(RecommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recommendation
            {
                Id = RecommendationId,
                Type = RecommendationTypes.ResourceMatch,
                TargetId = RequestId,
                Source = RecommendationSources.Rule
            });
        return mock.Object;
    }
}
