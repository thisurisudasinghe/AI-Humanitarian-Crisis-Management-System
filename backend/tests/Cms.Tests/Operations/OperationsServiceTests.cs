using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.Operations;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cms.Tests.Operations;

/// <summary>Decision-gate, stock-guard and retry-safe Phase 3 rules of OperationsService.</summary>
public class OperationsServiceTests
{
    private const string Key = "idem-allocation-001";

    [Fact]
    public async Task Allocate_ApprovedDecision_DecrementsAvailableOnceAndEnqueuesNotification()
    {
        var h = SeedAllocationScenario(available: 10, quantity: 4);

        var result = await h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None);

        Assert.Equal(OpsIds.Request, result.RequestId);
        Assert.Equal(OpsIds.Inventory, result.InventoryId);
        Assert.Equal(4, result.Quantity);
        Assert.Equal("Allocated", result.Status);
        Assert.Equal(OpsIds.Decision, result.DecisionId);

        var inventory = await h.Db.Inventories.SingleAsync();
        Assert.Equal(6, inventory.Available);
        Assert.Equal(4, inventory.Allocated);
        Assert.Single(await h.Db.Allocations.ToListAsync());
        Assert.Equal(Key, (await h.Db.Allocations.SingleAsync()).IdempotencyKey);

        h.DecisionGate.Verify(g => g.RequireApprovedAsync(OpsIds.Decision, RecommendationType.ResourceMatch,
            OpsIds.Request, It.IsAny<CancellationToken>()), Times.Once);
        h.Outbox.Verify(o => o.EnqueueAsync(OutboxEventTypes.NotificationSend, It.IsAny<Guid>(),
            It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Status.Verify(s => s.TransitionAsync(OpsIds.Request, RequestStatus.Allocated,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Allocate_InsufficientStock_FailsWithConflictAndLeavesStockUntouched()
    {
        var h = SeedAllocationScenario(available: 2, quantity: 5);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Equal("INSUFFICIENT_STOCK", ex.Code);
        Assert.Equal(2, (await h.Db.Inventories.SingleAsync()).Available);
        Assert.Equal(0, (await h.Db.Inventories.SingleAsync()).Allocated);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
        h.Outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Allocate_GateRejects_FailsClosedWithoutWritingAnything()
    {
        var h = SeedAllocationScenario(available: 10, quantity: 4);
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Decision is not approved.", "DECISION_REQUIRED"));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Equal("DECISION_REQUIRED", ex.Code);
        Assert.Equal(10, (await h.Db.Inventories.SingleAsync()).Available);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
        h.Outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Allocate_MissingDecisionRecord_FailsClosedBeforeCallingGate()
    {
        var h = OpsHarness.Create();
        h.Db.Inventories.Add(OpsSeed.Inventory(10));
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Equal("DECISION_REQUIRED", ex.Code);
        h.DecisionGate.Verify(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
    }

    [Fact]
    public async Task Allocate_RejectedDecisionRecord_FailsClosedBeforeCallingGate()
    {
        var h = OpsHarness.Create();
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision,
            OpsSeed.AllocationPayload(OpsIds.Request, OpsIds.Inventory, 2), DecisionOutcome.Rejected));
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Equal("DECISION_REQUIRED", ex.Code);
        h.DecisionGate.Verify(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    // Missing inventoryId.
    [InlineData("{\"requestId\":\"55555555-5555-5555-5555-555555555555\",\"quantity\":2}")]
    // Zero and negative quantity.
    [InlineData("{\"requestId\":\"55555555-5555-5555-5555-555555555555\",\"inventoryId\":\"44444444-4444-4444-4444-444444444444\",\"quantity\":0}")]
    [InlineData("{\"requestId\":\"55555555-5555-5555-5555-555555555555\",\"inventoryId\":\"44444444-4444-4444-4444-444444444444\",\"quantity\":-3}")]
    // Bad request id and malformed JSON.
    [InlineData("{\"requestId\":\"not-a-guid\",\"inventoryId\":\"44444444-4444-4444-4444-444444444444\",\"quantity\":2}")]
    [InlineData("not json")]
    public async Task Allocate_InvalidDecisionPayload_FailsClosed(string payload)
    {
        var h = OpsHarness.Create();
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision, payload));
        h.Db.Inventories.Add(OpsSeed.Inventory(10));
        await h.Db.SaveChangesAsync();
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpsSeed.Decision(OpsIds.Decision, payload));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Equal("INVALID_DECISION_PAYLOAD", ex.Code);
        Assert.Equal(10, (await h.Db.Inventories.SingleAsync()).Available);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
    }

    [Fact]
    public async Task Allocate_NonNumericQuantity_FailsClosedButLeaksRawJsonException()
    {
        // Documents current production behaviour: TryDecimal calls GetDecimal() without
        // guarding JsonValueKind, so a string quantity escapes as InvalidOperationException
        // instead of the domain code INVALID_DECISION_PAYLOAD. Reported as a bug; no
        // production code was changed.
        var payload = "{\"requestId\":\"55555555-5555-5555-5555-555555555555\"," +
            "\"inventoryId\":\"44444444-4444-4444-4444-444444444444\",\"quantity\":\"abc\"}";
        var h = OpsHarness.Create();
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision, payload));
        h.Db.Inventories.Add(OpsSeed.Inventory(10));
        await h.Db.SaveChangesAsync();
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpsSeed.Decision(OpsIds.Decision, payload));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None));

        Assert.Contains("requires an element of type 'Number'", ex.Message);
        Assert.Equal(10, (await h.Db.Inventories.SingleAsync()).Available);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
    }

    [Fact]
    public async Task Allocate_RepeatedIdempotencyKey_ReturnsExistingAllocationWithoutSecondDecrement()
    {
        var h = SeedAllocationScenario(available: 10, quantity: 4);

        var first = await h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None);
        var second = await h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await h.Db.Allocations.ToListAsync());
        Assert.Equal(6, (await h.Db.Inventories.SingleAsync()).Available);
        h.DecisionGate.Verify(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        h.Outbox.Verify(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Allocate_Phase3Failure_KeepsAllocationAndIsRetryable()
    {
        var h = SeedAllocationScenario(available: 10, quantity: 4);
        h.Status.SetupSequence(s => s.TransitionAsync(OpsIds.Request, RequestStatus.Allocated,
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status service unavailable"))
            .Returns(Task.CompletedTask);

        var allocation = await h.Service.AllocateAsync(OpsIds.Decision, Key, CancellationToken.None);

        // Phase 1 and 2 remain committed.
        Assert.Single(await h.Db.Allocations.ToListAsync());
        Assert.Equal(6, (await h.Db.Inventories.SingleAsync()).Available);
        h.Audit.Verify(a => a.LogAsync(OpsIds.User, "Allocation.StatusTransitionFailed", "Request",
            OpsIds.Request.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);

        var pending = await h.Service.GetPendingTransitionsAsync(CancellationToken.None);
        var item = Assert.Single(pending);
        Assert.Equal(allocation.Id, item.EntityId);
        Assert.Equal("Allocation", item.Operation);
        Assert.Equal(nameof(RequestStatus.Allocated), item.TargetStatus);

        var retried = await h.Service.RetryStatusTransitionAsync(allocation.Id, CancellationToken.None);

        Assert.NotNull(retried);
        Assert.Equal(allocation.Id, retried.Id);
        Assert.NotNull(retried.CompletedAt);
        h.Status.Verify(s => s.TransitionAsync(OpsIds.Request, RequestStatus.Allocated,
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        h.Audit.Verify(a => a.LogAsync(OpsIds.User, "Allocation.StatusTransitionRetried", "Allocation",
            allocation.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AllocateShelter_FullShelter_IsRejectedAndOccupancyIsUnchanged()
    {
        var h = OpsHarness.Create();
        h.Db.Shelters.Add(OpsSeed.Shelter(capacity: 3, occupancy: 2));
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision,
            OpsSeed.ShelterPayload(OpsIds.Request, OpsIds.Shelter, 2)));
        await h.Db.SaveChangesAsync();
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpsSeed.Decision(OpsIds.Decision,
                OpsSeed.ShelterPayload(OpsIds.Request, OpsIds.Shelter, 2)));

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => h.Service.AllocateShelterAsync(OpsIds.Decision, CancellationToken.None));

        Assert.Equal("SHELTER_FULL", ex.Code);
        Assert.Equal(2, (await h.Db.Shelters.SingleAsync()).Occupancy);
        Assert.Empty(await h.Db.ShelterAllocations.ToListAsync());
    }

    [Fact]
    public async Task AllocateShelter_ApprovedDecision_IncrementsOccupancyByHouseholdSize()
    {
        var h = OpsHarness.Create();
        h.Db.Shelters.Add(OpsSeed.Shelter(capacity: 5, occupancy: 1));
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision,
            OpsSeed.ShelterPayload(OpsIds.Request, OpsIds.Shelter, 3)));
        await h.Db.SaveChangesAsync();
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpsSeed.Decision(OpsIds.Decision,
                OpsSeed.ShelterPayload(OpsIds.Request, OpsIds.Shelter, 3)));

        var result = await h.Service.AllocateShelterAsync(OpsIds.Decision, CancellationToken.None);

        Assert.Equal(3, result.HouseholdSize);
        Assert.Equal(4, (await h.Db.Shelters.SingleAsync()).Occupancy);
        Assert.Single(await h.Db.ShelterAllocations.ToListAsync());
        h.DecisionGate.Verify(g => g.RequireApprovedAsync(OpsIds.Decision, RecommendationType.ShelterMatch,
            OpsIds.Request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Dispatch_GateRejects_FailsClosedWithoutMovingInventoryBuckets()
    {
        var h = OpsHarness.Create();
        var inventory = OpsSeed.Inventory(6);
        inventory.Allocated = 4;
        h.Db.Inventories.Add(inventory);
        h.Db.Allocations.Add(new Allocation
        {
            Id = OpsIds.InventoryB,
            RequestId = OpsIds.Request,
            InventoryId = OpsIds.Inventory,
            Quantity = 4,
            DecisionId = OpsIds.Decision,
            Status = "Allocated",
            IdempotencyKey = "key"
        });
        await h.Db.SaveChangesAsync();
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Decision is not approved.", "DECISION_REQUIRED"));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => h.Service.DispatchAsync(OpsIds.Decision, OpsIds.InventoryB, CancellationToken.None));

        Assert.Equal("DECISION_REQUIRED", ex.Code);
        Assert.Empty(await h.Db.Dispatches.ToListAsync());
        Assert.Equal(4, (await h.Db.Inventories.SingleAsync()).Allocated);
        Assert.Equal(0, (await h.Db.Inventories.SingleAsync()).Dispatched);
    }

    [Fact]
    public async Task CreateAssistanceRequest_NonPrivilegedUserCannotChooseAnotherSourceOrganisation()
    {
        var h = OpsHarness.Create();
        h.User.SetupGet(x => x.Roles).Returns(new[] { Cms.Domain.Constants.RoleNames.ShelterAdmin });
        h.User.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string role) => role == Cms.Domain.Constants.RoleNames.ShelterAdmin);
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.CreateAssistanceRequestAsync(
            new CreateAssistanceRequestInput(OpsIds.OtherOrg, null, OpsIds.FoodType, 3, "Shelter needs supplies", null),
            CancellationToken.None));

        Assert.Equal("ASSISTANCE_SOURCE_ORG_REQUIRED", ex.Code);
        Assert.Empty(await h.Db.AssistanceRequests.ToListAsync());
    }

    [Fact]
    public async Task RespondAssistanceRequest_NonPrivilegedUserCannotRespondWhenNoTargetOrganisationExists()
    {
        var h = OpsHarness.Create();
        h.User.SetupGet(x => x.Roles).Returns(new[] { Cms.Domain.Constants.RoleNames.PartnerOfficer });
        h.User.Setup(x => x.IsInRole(It.IsAny<string>())).Returns((string role) => role == Cms.Domain.Constants.RoleNames.PartnerOfficer);
        h.Db.AssistanceRequests.Add(new AssistanceRequest
        {
            Id = OpsIds.Task,
            FromOrgId = OpsIds.OtherOrg,
            ToOrgId = null,
            ResourceTypeId = OpsIds.FoodType,
            Quantity = 3,
            Reason = "Shelter request",
            Status = "Pending"
        });
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.RespondAssistanceRequestAsync(
            OpsIds.Task, true, "Accepted", CancellationToken.None));

        Assert.Equal("ASSISTANCE_TARGET_ORG_REQUIRED", ex.Code);
        Assert.Equal("Pending", (await h.Db.AssistanceRequests.SingleAsync()).Status);
    }

    private static OpsHarness SeedAllocationScenario(decimal available, decimal quantity)
    {
        var h = OpsHarness.Create();
        var payload = OpsSeed.AllocationPayload(OpsIds.Request, OpsIds.Inventory, quantity);
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.Add(OpsSeed.Inventory(available));
        h.Db.DecisionRecords.Add(OpsSeed.DecisionRecord(OpsIds.Decision, payload));
        h.Db.Requests.Add(OpsSeed.Request(OpsIds.Request, Urgency.Critical));
        h.DecisionGate.Setup(g => g.RequireApprovedAsync(It.IsAny<Guid>(), It.IsAny<RecommendationType>(),
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OpsSeed.Decision(OpsIds.Decision, payload));
        h.Db.SaveChanges();
        return h;
    }
}
