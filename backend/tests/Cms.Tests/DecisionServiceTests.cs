using Cms.Application.DTOs.AI;
using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Services.Governance;
using Moq;
using CurrentUserContract = Cms.Application.Contracts.ICurrentUser;

namespace Cms.Tests;

public class DecisionServiceTests
{
    private static readonly Guid RecommendationId = Guid.NewGuid();
    private static readonly Guid TargetRequestId = Guid.NewGuid();

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

    [Fact]
    public async Task RecordAsync_ApprovedResourceMatch_TransitionsRecommendedRequest()
    {
        using var context = TestDb.Create();
        var recommendation = MakeRecommendation();
        context.Recommendations.Add(recommendation);
        context.Requests.Add(new Request
        {
            Id = TargetRequestId,
            RequesterUserId = Guid.NewGuid(),
            Category = Cms.Domain.Enums.Cases.Category.Food,
            Urgency = Cms.Domain.Enums.Cases.Urgency.High,
            Status = RequestStatus.Recommended,
            SubmittedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var requestStatus = new Mock<IRequestStatusService>();
        requestStatus
            .Setup(s => s.TransitionAsync(
                It.IsAny<Guid>(),
                It.IsAny<RequestStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = MakeService(context, Roles.LogisticsManager, requestStatus.Object);

        await service.RecordAsync(
            RecommendationId,
            new DecisionRequestDto { Outcome = "Approved", Reason = "Verified" });

        requestStatus.Verify(
            s => s.TransitionAsync(
                TargetRequestId,
                RequestStatus.Approved,
                It.Is<string?>(reason => reason != null && reason.Contains("ResourceMatch")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Recommendation MakeRecommendation() => new()
    {
        Id = RecommendationId,
        Type = RecommendationTypes.ResourceMatch,
        TargetId = TargetRequestId,
        PayloadJson = "{}",
        Confidence = 0.8m,
        Rationale = "test",
        Source = RecommendationSources.Rule
    };

    private static DecisionService MakeService(
        CmsDbContext context,
        string role,
        IRequestStatusService? requestStatusService = null)
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

        var requestStatus = new Mock<IRequestStatusService>();
        requestStatus
            .Setup(s => s.TransitionAsync(
                It.IsAny<Guid>(),
                It.IsAny<RequestStatus>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new DecisionService(
            context,
            recommendations.Object,
            currentUser.Object,
            audit.Object,
            new DateTimeService(),
            requestStatusService ?? requestStatus.Object);
    }
}
