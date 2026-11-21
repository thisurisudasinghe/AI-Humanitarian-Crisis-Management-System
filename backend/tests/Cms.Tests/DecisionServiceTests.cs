using Cms.Application.DTOs.AI;
using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Services.Governance;
using Moq;
using CurrentUserContract = Cms.Application.Contracts.ICurrentUser;

namespace Cms.Tests;

public class DecisionServiceTests
{
    private static readonly Guid RecommendationId = Guid.NewGuid();

    [Fact]
    public async Task RecordAsync_WithAuthorisedRole_RecordsApprovedDecision()
    {
        using var context = TestDb.Create();
        context.Recommendations.Add(MakeRecommendation());
        await context.SaveChangesAsync();

        var service = MakeService(context, Roles.EmergencyCoordinator);

        var result = await service.RecordAsync(
            RecommendationId,
            new DecisionRequestDto
            {
                Outcome = "Approved",
                Reason = "Verified by coordinator"
            });

        Assert.Equal(DecisionOutcome.Approved, result.Outcome);
        Assert.Single(context.DecisionRecords);
        Assert.Equal("Approved", context.DecisionRecords.Single().Outcome.ToString());
    }

    [Fact]
    public async Task RecordAsync_Twice_IsRejectedBecauseRecordsAreImmutable()
    {
        using var context = TestDb.Create();
        context.Recommendations.Add(MakeRecommendation());
        await context.SaveChangesAsync();

        var service = MakeService(context, Roles.LogisticsManager);
        var request = new DecisionRequestDto
        {
            Outcome = "Rejected",
            Reason = "Not applicable"
        };

        await service.RecordAsync(RecommendationId, request);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.RecordAsync(RecommendationId, request));
    }

    [Fact]
    public async Task RecordAsync_WithUnauthorisedRole_ThrowsForbidden()
    {
        using var context = TestDb.Create();
        context.Recommendations.Add(MakeRecommendation());
        await context.SaveChangesAsync();

        var service = MakeService(context, Roles.Citizen);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordAsync(
                RecommendationId,
                new DecisionRequestDto { Outcome = "Approved" }));
    }

    [Fact]
    public async Task RecordAsync_WithNonFrozenOutcome_ThrowsValidation()
    {
        using var context = TestDb.Create();
        context.Recommendations.Add(MakeRecommendation());
        await context.SaveChangesAsync();

        var service = MakeService(context, Roles.SystemAdmin);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordAsync(
                RecommendationId,
                new DecisionRequestDto { Outcome = "Approve" }));
    }

    [Fact]
    public async Task RecordAsync_ModifiedWithoutReason_ThrowsValidation()
    {
        using var context = TestDb.Create();
        context.Recommendations.Add(MakeRecommendation());
        await context.SaveChangesAsync();

        var service = MakeService(context, Roles.SystemAdmin);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.RecordAsync(
                RecommendationId,
                new DecisionRequestDto { Outcome = "Modified" }));
    }

    private static Recommendation MakeRecommendation() => new()
    {
        Id = RecommendationId,
        Type = RecommendationTypes.ResourceMatch,
        TargetId = Guid.NewGuid(),
        PayloadJson = "{}",
        Confidence = 0.8m,
        Rationale = "test",
        Source = RecommendationSources.Rule
    };

    private static DecisionService MakeService(CmsDbContext context, string role)
    {
        var userId = Guid.NewGuid();

        var currentUser = new Mock<CurrentUserContract>();
        currentUser.SetupGet(u => u.UserId).Returns(userId);
        currentUser.SetupGet(u => u.Roles).Returns(new[] { role });

        var audit = new Mock<Cms.Application.Contracts.IAuditLogger>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var recommendations = new Mock<IRecommendationService>();
        recommendations
            .Setup(s => s.GetByIdAsync(RecommendationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRecommendation());

        return new DecisionService(
            context,
            recommendations.Object,
            currentUser.Object,
            audit.Object,
            new DateTimeService());
    }
}
