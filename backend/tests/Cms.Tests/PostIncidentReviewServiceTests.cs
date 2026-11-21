using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services.Governance;

namespace Cms.Tests;

public class PostIncidentReviewServiceTests
{
    [Fact]
    public async Task GetDecisionsAsync_IncludesFinalRequestStatus_AndAppliesFilters()
    {
        using var context = TestDb.Create();
        var crisisId = Guid.NewGuid();
        var request = new Request
        {
            Id = Guid.NewGuid(),
            RequesterUserId = Guid.NewGuid(),
            CrisisId = crisisId,
            Category = Category.Food,
            Urgency = Urgency.High,
            Status = RequestStatus.Approved,
            SubmittedAt = DateTime.UtcNow
        };
        var recommendation = new Recommendation
        {
            Id = Guid.NewGuid(),
            Type = RecommendationTypes.ResourceMatch,
            TargetId = request.Id,
            PayloadJson = "{}",
            Confidence = 0.8m,
            Rationale = "test",
            Source = RecommendationSources.Rule,
            CreatedAt = DateTime.UtcNow
        };
        var decision = new DecisionRecord
        {
            Id = Guid.NewGuid(),
            RecommendationId = recommendation.Id,
            Outcome = DecisionOutcome.Modified,
            Reason = "Changed quantity",
            DecidedByUserId = Guid.NewGuid(),
            DecidedAt = DateTime.UtcNow
        };

        context.Requests.Add(request);
        context.Recommendations.Add(recommendation);
        context.DecisionRecords.Add(decision);
        await context.SaveChangesAsync();

        var service = new PostIncidentReviewService(context);

        var all = await service.GetDecisionsAsync(crisisId);
        var entry = Assert.Single(all.Items);
        Assert.Equal(RequestStatus.Approved.ToString(), entry.FinalRequestStatus);
        Assert.True(entry.IsOverride);

        var filtered = await service.GetDecisionsAsync(
            crisisId,
            outcome: "Modified",
            fromUtc: DateTime.UtcNow.AddMinutes(-1),
            toUtc: DateTime.UtcNow.AddMinutes(1));
        Assert.Single(filtered.Items);

        var noMatch = await service.GetDecisionsAsync(crisisId, outcome: "Rejected");
        Assert.Empty(noMatch.Items);
    }
}
