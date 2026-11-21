using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums;
using Cms.Infrastructure.Services.Governance;

namespace Cms.Tests;

public class PostIncidentReviewServiceTests
{
    [Fact]
    public async Task GetDecisionsAsync_IncludesCrisisSummaryAndRequestRecommendations()
    {
        var crisisId = Guid.NewGuid();

        using var db = TestDb.Create();
        db.Recommendations.Add(new Recommendation
        {
            Type = RecommendationTypes.Summary,
            TargetId = crisisId,
            PayloadJson = "{}",
            Confidence = 0.9m,
            Rationale = "Crisis summary",
            Source = RecommendationSources.Rule
        });

        var request = new Request
        {
            CrisisId = crisisId,
            Description = "Flood assistance needed"
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync();

        var resourceRecommendation = new Recommendation
        {
            Type = RecommendationTypes.ResourceMatch,
            TargetId = request.Id,
            PayloadJson = "{}",
            Confidence = 0.8m,
            Rationale = "Resource match",
            Source = RecommendationSources.Rule
        };
        db.Recommendations.Add(resourceRecommendation);
        db.DecisionRecords.Add(new DecisionRecord
        {
            RecommendationId = resourceRecommendation.Id,
            Outcome = DecisionOutcome.Rejected,
            Reason = "Not safe",
            DecidedByUserId = Guid.NewGuid(),
            DecidedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new PostIncidentReviewService(db);

        var result = await service.GetDecisionsAsync(crisisId);

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, item => item.Type == RecommendationTypes.Summary);
        Assert.Contains(result.Items, item => item.Type == RecommendationTypes.ResourceMatch);
        Assert.Contains(result.Items, item => item.IsOverride);
    }
}
