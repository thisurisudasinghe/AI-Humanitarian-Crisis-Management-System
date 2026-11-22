using Cms.Application.DTOs.Donations;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Services;
using Moq;

namespace Cms.Tests;

public sealed class KpiServiceTests
{
    [Fact]
    public async Task GetReportAsync_ComputesLiveOperationalMetrics()
    {
        using var db = TestDb.Create();
        var now = DateTime.UtcNow;
        var areaId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var closedRequestId = Guid.NewGuid();
        var recommendationId = Guid.NewGuid();

        db.Set<Area>().Add(new Area
        {
            Id = areaId,
            CrisisId = Guid.NewGuid(),
            Name = "Colombo",
            Lat = 6.93m,
            Lng = 79.84m
        });

        db.Requests.AddRange(
            new Request
            {
                Id = requestId,
                RequesterUserId = Guid.NewGuid(),
                CrisisId = Guid.NewGuid(),
                AreaId = areaId,
                Category = Category.Food,
                Urgency = Urgency.High,
                Status = RequestStatus.Submitted,
                SubmittedAt = now.AddHours(-1),
                VerifiedAt = now
            },
            new Request
            {
                Id = closedRequestId,
                RequesterUserId = Guid.NewGuid(),
                CrisisId = Guid.NewGuid(),
                AreaId = areaId,
                Category = Category.Medicine,
                Urgency = Urgency.Low,
                Status = RequestStatus.Closed,
                SubmittedAt = now.AddMinutes(-30),
                ClosedAt = now
            });

        db.Inventories.Add(new Inventory
        {
            Quantity = 100m,
            Available = 60m,
            Reserved = 10m,
            Allocated = 0m,
            Dispatched = 5m,
            Utilised = 25m,
            ReorderThreshold = 10m
        });

        db.Shelters.AddRange(
            new Shelter
            {
                Name = "Approved centre",
                Capacity = 100,
                Occupancy = 40,
                Approved = true
            },
            new Shelter
            {
                Name = "Unapproved centre",
                Capacity = 50,
                Occupancy = 50,
                Approved = false
            });

        db.ShelterAllocations.Add(new ShelterAllocation
        {
            RequestId = requestId,
            ShelterId = db.Shelters.Local.First(shelter => shelter.Approved).Id,
            HouseholdSize = 2,
            DecisionId = Guid.NewGuid()
        });

        db.Recommendations.Add(new Recommendation
        {
            Id = recommendationId,
            Type = "ResourceMatch",
            TargetId = requestId,
            PayloadJson = "{}",
            Confidence = 0.8m,
            Rationale = "test",
            Source = "Rule"
        });

        db.DecisionRecords.AddRange(
            new DecisionRecord
            {
                RecommendationId = recommendationId,
                Outcome = DecisionOutcome.Modified,
                Reason = "Changed quantity",
                DecidedByUserId = Guid.NewGuid(),
                DecidedAt = now.AddMinutes(-10)
            },
            new DecisionRecord
            {
                RecommendationId = recommendationId,
                Outcome = DecisionOutcome.Approved,
                Reason = "Accepted",
                DecidedByUserId = Guid.NewGuid(),
                DecidedAt = now.AddMinutes(-5)
            });

        await db.SaveChangesAsync();

        var donations = new Mock<IDonationService>();
        donations
            .Setup(service => service.GetReportAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DonationReportDto());

        var service = new KpiService(db, donations.Object, new DateTimeService());

        var report = await service.GetReportAsync(
            now.AddHours(-2),
            now.AddHours(2),
            null,
            null);

        Assert.False(report.IsPlaceholder);
        Assert.Empty(report.PendingSections);
        Assert.Contains("cases", report.AvailableSections);
        Assert.Contains("resources", report.AvailableSections);
        Assert.Contains("shelter", report.AvailableSections);
        Assert.Contains("ai", report.AvailableSections);

        Assert.Equal(1, report.UnresolvedRequestCount);
        Assert.Equal(1d, report.AverageResponseTimeHours!.Value);
        Assert.Equal(25d, report.ResourceUtilisationPercent!.Value);
        Assert.Equal(10, report.ReservedResources!.Value);
        Assert.Equal(5, report.DispatchedResources!.Value);
        Assert.Equal(40d, report.ShelterOccupancyPercent!.Value);
        Assert.Equal(0, report.SheltersAtCapacity!.Value);
        Assert.Equal(50d, report.AiOverrideRatePercent!.Value);

        var requestAreas = Assert.Single(report.RequestsByArea);
        Assert.Equal(areaId, requestAreas.AreaId);
        Assert.Equal("Colombo", requestAreas.AreaName);
        Assert.Equal(2, requestAreas.Count);

        var shelterAreas = Assert.Single(report.SheltersByArea);
        Assert.Equal(areaId, shelterAreas.AreaId);
        Assert.Equal(1, shelterAreas.Count);
    }
}
