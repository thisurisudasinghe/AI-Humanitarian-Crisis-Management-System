using System.Net;
using System.Net.Http.Json;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Integration;

/// <summary>
/// Route-level evidence for the exact Member 3 governance endpoints and RBAC.
/// </summary>
public class GovernanceEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public GovernanceEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Recommendation_routes_require_a_staff_role()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/recommendations")).StatusCode);

        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/recommendations")).StatusCode);

        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await officer.GetAsync("/api/v1/recommendations")).StatusCode);
    }

    [Fact]
    public async Task Exact_decision_route_stores_a_decision_for_an_authorised_decider()
    {
        var recommendationId = await SeedRecommendationAsync();
        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);

        var response = await officer.PostAsJsonAsync(
            $"/api/v1/recommendations/{recommendationId}/decision",
            new { outcome = "Approved", finalPayloadJson = "{\"category\":\"Food\"}", reason = "Confirmed" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var decision = Assert.Single(db.DecisionRecords.AsNoTracking());
        Assert.Equal(recommendationId, decision.RecommendationId);
        Assert.Equal(DecisionOutcome.Approved, decision.Outcome);
    }

    [Fact]
    public async Task SystemAdmin_has_no_break_glass_decision_right()
    {
        var recommendationId = await SeedRecommendationAsync();
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/recommendations/{recommendationId}/decision",
            new { outcome = "Approved", reason = "Admin override" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_incident_review_route_requires_a_staff_role()
    {
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/post-incident-review")).StatusCode);

        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);
        Assert.Equal(HttpStatusCode.OK,
            (await officer.GetAsync("/api/v1/post-incident-review")).StatusCode);
    }

    private async Task<Guid> SeedRecommendationAsync()
    {
        await _factory.EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var recommendation = new Recommendation
        {
            Type = RecommendationTypes.Classification,
            TargetId = Guid.NewGuid(),
            PayloadJson = "{\"category\":\"Food\"}",
            Confidence = 0.9m,
            Rationale = "Offline rule classification",
            Source = RecommendationSources.Rule,
            Status = RecommendationStatuses.Pending
        };
        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();
        return recommendation.Id;
    }
}
