using Cms.Application.Contracts;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Operations;
using Microsoft.EntityFrameworkCore;
using Moq;
using MatchCandidate = Cms.Application.Contracts.Operations.MatchCandidate;

namespace Cms.Tests.Operations;

/// <summary>D4-12 item 2: read-only, deterministic matcher behaviour and score bounds.</summary>
public class OperationsMatcherTests
{
    [Fact]
    public async Task ResourceMatcher_ExcludesZeroStockAndExpiredStock_AndLabelsPartialFulfilment()
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.AddRange(
            OpsSeed.Inventory(20, accessible: true),
            OpsSeed.Inventory(8, id: OpsIds.InventoryB),
            OpsSeed.Inventory(0, id: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001")),
            OpsSeed.Inventory(5, id: Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"),
                expiry: DateTime.UtcNow.AddDays(-1)));
        await h.Db.SaveChangesAsync();
        SetupSummary(h, OpsSeed.Summary(OpsIds.Request, Urgency.Critical,
            description: "10 packs", accessibility: "wheelchair step free access needed"));
        var matcher = new ResourceMatcher(h.Db, h.Geo.Object, h.RequestReader.Object, h.User.Object);

        var first = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);
        var second = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.All(first, c =>
        {
            Assert.InRange(c.Score, 0m, 1m);
            Assert.True(c.IsApproximate);
        });
        Assert.Equal(OpsIds.Inventory, first[0].TargetId);
        Assert.Contains("partial: 8 of 10", first[1].Rationale);
        Assert.Equal(first.Select(c => c.TargetId), second.Select(c => c.TargetId));
        Assert.Equal(first.Select(c => c.Score), second.Select(c => c.Score));
        Assert.DoesNotContain(first, c => c.TargetId == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
        Assert.DoesNotContain(first, c => c.TargetId == Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"));

        // Matching is read-only.
        Assert.All(await h.Db.Inventories.ToListAsync(), i => Assert.Equal(i.Quantity, i.Available));
        Assert.Empty(await h.Db.Allocations.ToListAsync());
    }

    [Fact]
    public async Task ResourceMatcher_PropagatesPreciseRouteFlagAndShowsDistance()
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.Add(OpsSeed.Inventory(10));
        await h.Db.SaveChangesAsync();
        SetupSummary(h, OpsSeed.Summary(OpsIds.Request, Urgency.High, description: "10 packs"));
        h.Geo.Setup(g => g.GetDistanceAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DistanceResult(7d, 20d, false));
        var matcher = new ResourceMatcher(h.Db, h.Geo.Object, h.RequestReader.Object, h.User.Object);

        var result = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);

        var candidate = Assert.Single(result);
        Assert.False(candidate.IsApproximate);
        Assert.Contains("7 km", candidate.Rationale);
        Assert.InRange(candidate.Score, 0m, 1m);
    }

    [Fact]
    public async Task ResourceMatcher_UnknownRequest_ReturnsEmpty()
    {
        var h = OpsHarness.Create();
        SetupSummary(h, null);
        var matcher = new ResourceMatcher(h.Db, h.Geo.Object, h.RequestReader.Object, h.User.Object);

        Assert.Empty(await matcher.RankAsync(OpsIds.Request, CancellationToken.None));
    }

    [Fact]
    public async Task ShelterMatcher_ExcludesFullAndUnapprovedShelters_AndIsDeterministic()
    {
        var h = OpsHarness.Create();
        var full = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var unapproved = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
        var tight = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
        h.Db.Shelters.AddRange(
            OpsSeed.Shelter(capacity: 10, occupancy: 2),
            OpsSeed.Shelter(capacity: 6, occupancy: 4, id: full),
            OpsSeed.Shelter(capacity: 20, occupancy: 0, approved: false, id: unapproved),
            OpsSeed.Shelter(capacity: 8, occupancy: 4, id: tight));
        await h.Db.SaveChangesAsync();
        SetupSummary(h, OpsSeed.Summary(OpsIds.Request, Urgency.High,
            category: Category.Shelter, description: "household of 4"));
        var matcher = new ShelterRecommender(h.Db, h.Geo.Object, h.RequestReader.Object, h.User.Object);

        var first = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);
        var second = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(OpsIds.Shelter, first[0].TargetId);
        Assert.Equal(tight, first[1].TargetId);
        Assert.All(first, c =>
        {
            Assert.InRange(c.Score, 0m, 1m);
            Assert.True(c.IsApproximate);
            Assert.Contains("places free", c.Rationale);
        });
        Assert.Equal(first.Select(c => c.TargetId), second.Select(c => c.TargetId));
        Assert.DoesNotContain(first, c => c.TargetId == full);
        Assert.DoesNotContain(first, c => c.TargetId == unapproved);
        Assert.Equal(2, (await h.Db.Shelters.SingleAsync(s => s.Id == OpsIds.Shelter)).Occupancy);
        Assert.Empty(await h.Db.ShelterAllocations.ToListAsync());
    }

    [Fact]
    public async Task VolunteerMatcher_ExcludesUnverifiedInactiveAndUnavailable_AndNamesMatchedSkills()
    {
        var h = OpsHarness.Create();
        var unverified = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var inactive = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
        var unavailable = Guid.Parse("cccccccc-0000-0000-0000-000000000003");
        var lowSkill = Guid.Parse("cccccccc-0000-0000-0000-000000000004");
        var now = DateTime.UtcNow;
        h.Db.Volunteers.AddRange(
            new Volunteer { Id = OpsIds.Volunteer, UserId = Guid.NewGuid(), Active = true,
                VerificationStatus = "Verified", SkillsJson = "[\"medical\",\"first aid\"]" },
            new Volunteer { Id = unverified, UserId = Guid.NewGuid(), Active = true,
                VerificationStatus = "Pending", SkillsJson = "[\"medical\"]" },
            new Volunteer { Id = inactive, UserId = Guid.NewGuid(), Active = false,
                VerificationStatus = "Verified", SkillsJson = "[\"medical\"]" },
            new Volunteer { Id = unavailable, UserId = Guid.NewGuid(), Active = true,
                VerificationStatus = "Verified", SkillsJson = "[\"medical\"]" },
            new Volunteer { Id = lowSkill, UserId = Guid.NewGuid(), Active = true,
                VerificationStatus = "Verified", SkillsJson = "[\"driving\"]" });
        h.Db.VolunteerAvailabilities.AddRange(
            Availability(OpsIds.Volunteer, now), Availability(unverified, now),
            Availability(inactive, now), Availability(lowSkill, now));
        await h.Db.SaveChangesAsync();
        SetupSummary(h, OpsSeed.Summary(OpsIds.Request, Urgency.Critical,
            description: "medical first aid needed"));
        var matcher = new VolunteerMatcher(h.Db, h.Geo.Object, h.RequestReader.Object);

        var first = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);
        var second = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(OpsIds.Volunteer, first[0].TargetId);
        Assert.Equal(lowSkill, first[1].TargetId);
        Assert.All(first, c =>
        {
            Assert.InRange(c.Score, 0m, 1m);
            Assert.True(c.IsApproximate);
        });
        Assert.Contains("matched skills: medical, first aid", first[0].Rationale);
        Assert.Contains("matched skills: none", first[1].Rationale);
        Assert.Equal(first.Select(c => c.TargetId), second.Select(c => c.TargetId));
        Assert.DoesNotContain(first, c => c.TargetId == unverified);
        Assert.DoesNotContain(first, c => c.TargetId == inactive);
        Assert.DoesNotContain(first, c => c.TargetId == unavailable);
        Assert.Empty(await h.Db.VolunteerTasks.ToListAsync());
    }

    [Fact]
    public async Task Matchers_OrderedByScoreThenId_WhenScoresTie()
    {
        var h = OpsHarness.Create();
        var lowerId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
        var higherId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.AddRange(
            OpsSeed.Inventory(10, id: higherId),
            OpsSeed.Inventory(10, id: lowerId));
        await h.Db.SaveChangesAsync();
        SetupSummary(h, OpsSeed.Summary(OpsIds.Request, Urgency.Critical, description: "10 packs"));
        var matcher = new ResourceMatcher(h.Db, h.Geo.Object, h.RequestReader.Object, h.User.Object);

        var first = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);
        var second = await matcher.RankAsync(OpsIds.Request, CancellationToken.None);

        Assert.Equal(2, first.Count);
        Assert.Equal(first[0].Score, first[1].Score);
        Assert.Equal(lowerId, first[0].TargetId);
        Assert.Equal(higherId, first[1].TargetId);
        Assert.Equal(first.Select(c => c.TargetId), second.Select(c => c.TargetId));
    }

    private static void SetupSummary(OpsHarness h, Cms.Application.DTOs.RequestSummaryDto? summary) =>
        h.RequestReader.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult(id == OpsIds.Request ? summary : null));

    private static VolunteerAvailability Availability(Guid volunteerId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        VolunteerId = volunteerId,
        StartsAt = now.AddMinutes(-30),
        EndsAt = now.AddMinutes(30)
    };
}
