using Cms.Application.Contracts;
using Cms.Application.DTOs.AI;
using Cms.Domain.Constants;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services.AI;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cms.Tests;

public class RecommendationServiceTests
{
    [Fact]
    public async Task CreateAsync_AuditsEveryRecommendationWrite()
    {
        using var db = TestDb.Create();
        var audit = new Mock<IAuditLogger>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new RecommendationService(db, audit.Object);
        var id = await service.CreateAsync(NewRequest());

        audit.Verify(a => a.LogAsync(
            "Recommendation.Created",
            "Recommendation",
            id.ToString(),
            It.IsAny<object?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecommendationRows_CannotBeModifiedOrDeleted()
    {
        using var db = TestDb.Create();
        var service = new RecommendationService(db, Mock.Of<IAuditLogger>());
        await service.CreateAsync(NewRequest());

        var stored = await db.Recommendations.SingleAsync();
        stored.Rationale = "changed after creation";

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static CreateRecommendationRequest NewRequest() => new()
    {
        Type = RecommendationTypes.Classification,
        TargetId = Guid.NewGuid(),
        PayloadJson = "{\"category\":\"Food\"}",
        Confidence = 0.8m,
        Rationale = "Offline rule classification",
        Source = RecommendationSources.Rule
    };
}
