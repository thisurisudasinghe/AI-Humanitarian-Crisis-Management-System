using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Operations;
using Moq;

namespace Cms.Tests.Operations;

/// <summary>Fixed identifiers keep every operations test deterministic.</summary>
internal static class OpsIds
{
    public static readonly Guid Org = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OtherOrg = Guid.Parse("11111111-1111-1111-1111-111111111112");
    public static readonly Guid User = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid FoodType = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid MedicineType = Guid.Parse("33333333-3333-3333-3333-333333333334");
    public static readonly Guid Inventory = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid InventoryB = Guid.Parse("44444444-4444-4444-4444-444444444445");
    public static readonly Guid Request = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid RequestB = Guid.Parse("55555555-5555-5555-5555-555555555556");
    public static readonly Guid RequestC = Guid.Parse("55555555-5555-5555-5555-555555555557");
    public static readonly Guid Decision = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid Shelter = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid Volunteer = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid Task = Guid.Parse("99999999-9999-9999-9999-999999999999");
}

/// <summary>Wires OperationsService with in-memory EF and loose Moq collaborators.</summary>
internal sealed class OpsHarness
{
    private OpsHarness()
    {
        Db = TestDb.Create();
        User = new Mock<ICurrentUser>();
        User.SetupGet(x => x.UserId).Returns(OpsIds.User);
        User.SetupGet(x => x.OrgId).Returns(OpsIds.Org);
        User.SetupGet(x => x.Roles).Returns(new[] { RoleNames.SystemAdmin });
        User.Setup(x => x.IsInRole(It.IsAny<string>()))
            .Returns((string role) => role is RoleNames.SystemAdmin or RoleNames.EmergencyCoordinator);
        User.SetupGet(x => x.IsAuthenticated).Returns(true);

        Audit = new Mock<IAuditLogger>();
        Audit.Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DecisionGate = new Mock<IDecisionGate>();
        RequestReader = new Mock<IRequestReader>();
        Status = new Mock<IRequestStatusService>();
        Status.Setup(s => s.TransitionAsync(It.IsAny<Guid>(), It.IsAny<Cms.Domain.Enums.Cases.RequestStatus>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Outbox = new Mock<IOutbox>();
        Outbox.Setup(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Geo = new Mock<IGeoService>();
        Geo.Setup(g => g.GetDistanceAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DistanceResult(10d, 12d, true));
        Geo.Setup(g => g.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeoPoint?)null);

        ResourceMatcher = new Mock<Cms.Application.Contracts.IResourceMatcher>();
        ShelterRecommender = new Mock<Cms.Application.Contracts.IShelterRecommender>();
        VolunteerMatcher = new Mock<Cms.Application.Contracts.IVolunteerMatcher>();

        Service = new OperationsService(Db, User.Object, Audit.Object, DecisionGate.Object,
            RequestReader.Object, Status.Object, Outbox.Object, Geo.Object,
            ResourceMatcher.Object, ShelterRecommender.Object, VolunteerMatcher.Object);
    }

    public static OpsHarness Create() => new();

    public CmsDbContext Db { get; }
    public Mock<ICurrentUser> User { get; }
    public Mock<IAuditLogger> Audit { get; }
    public Mock<IDecisionGate> DecisionGate { get; }
    public Mock<IRequestReader> RequestReader { get; }
    public Mock<IRequestStatusService> Status { get; }
    public Mock<IOutbox> Outbox { get; }
    public Mock<IGeoService> Geo { get; }
    public Mock<Cms.Application.Contracts.IResourceMatcher> ResourceMatcher { get; }
    public Mock<Cms.Application.Contracts.IShelterRecommender> ShelterRecommender { get; }
    public Mock<Cms.Application.Contracts.IVolunteerMatcher> VolunteerMatcher { get; }
    public OperationsService Service { get; }
}

internal static class OpsSeed
{
    public static ResourceType FoodType() => new()
    {
        Id = OpsIds.FoodType, Name = "Food packs", Unit = "packs"
    };

    public static Inventory Inventory(decimal available, Guid? id = null, Guid? typeId = null,
        DateTime? expiry = null, double? lat = 6.9, double? lng = 79.8, bool accessible = false) => new()
    {
        Id = id ?? OpsIds.Inventory,
        ResourceTypeId = typeId ?? OpsIds.FoodType,
        OrgId = OpsIds.Org,
        Quantity = available,
        Available = available,
        ReorderThreshold = 1,
        ExpiryDate = expiry,
        Lat = lat,
        Lng = lng,
        AccessibilityCapable = accessible
    };

    public static Shelter Shelter(int capacity, int occupancy, bool approved = true, Guid? id = null) => new()
    {
        Id = id ?? OpsIds.Shelter,
        Name = "Community shelter",
        Capacity = capacity,
        Occupancy = occupancy,
        OrgId = OpsIds.Org,
        Approved = approved,
        Lat = 6.9,
        Lng = 79.8,
        Facilities = "[]",
        AccessibilityFeatures = "[]",
        Services = "[]"
    };

    public static Request Request(Guid id, Urgency urgency, Category category = Category.Food,
        RequestStatus status = RequestStatus.Submitted, DateTime? created = null,
        string description = "3 packs", string? accessibility = null)
    {
        var stamp = created ?? DateTime.UtcNow.AddDays(-1);
        return new Request
        {
            Id = id,
            Category = category,
            Urgency = urgency,
            Status = status,
            Description = description,
            AccessibilityNeeds = accessibility,
            HouseholdSize = 3,
            Lat = 6.9m,
            Lng = 79.8m,
            AreaId = Guid.NewGuid(),
            RequesterUserId = OpsIds.User,
            SubmittedAt = stamp,
            CreatedAt = stamp
        };
    }

    public static Cms.Application.DTOs.RequestSummaryDto Summary(Guid id, Urgency urgency,
        Category category = Category.Food, DateTime? created = null, string description = "3 packs",
        string? accessibility = null) => new()
    {
        Id = id,
        Category = category,
        Urgency = urgency,
        Status = Cms.Domain.Enums.Cases.RequestStatus.Submitted,
        Description = description,
        AccessibilityNeeds = accessibility,
        HouseholdSize = 3,
        Lat = 6.9m,
        Lng = 79.8m,
        CreatedAt = created ?? DateTime.UtcNow.AddDays(-1)
    };

    public static DecisionRecord DecisionRecord(Guid id, string payload,
        DecisionOutcome outcome = DecisionOutcome.Approved) => new()
    {
        Id = id,
        RecommendationId = Guid.NewGuid(),
        Outcome = outcome,
        FinalPayloadJson = payload,
        Reason = "test decision",
        DecidedByUserId = OpsIds.User,
        DecidedAt = DateTime.UtcNow
    };

    public static DecisionDto Decision(Guid id, string payload,
        DecisionOutcome outcome = DecisionOutcome.Approved) =>
        new(id, outcome, payload, OpsIds.User, DateTime.UtcNow);

    public static string AllocationPayload(Guid requestId, Guid inventoryId, decimal quantity) =>
        JsonSerializer.Serialize(new { requestId, inventoryId, quantity });

    public static string ShelterPayload(Guid requestId, Guid shelterId, int householdSize) =>
        JsonSerializer.Serialize(new { requestId, shelterId, householdSize });
}
