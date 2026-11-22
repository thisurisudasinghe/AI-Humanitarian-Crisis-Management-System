using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.Operations;
using Cms.Domain.Entities.Operations;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cms.Tests.Operations;

/// <summary>D4-12 item 2: service-level validation guards for Member 4's own rules.</summary>
public class OperationsValidationTests
{
    [Fact]
    public async Task Allocate_MissingIdempotencyKey_FailsClosedWithDedicatedCode()
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, "   ", CancellationToken.None));

        Assert.Equal("IDEMPOTENCY_KEY_REQUIRED", ex.Code);
        Assert.Contains("Idempotency-Key header is required", ex.Message);
        Assert.Empty(h.Db.Allocations);
        h.Audit.Verify(a => a.LogAsync(OpsIds.User, "Allocation.Rejected", "Allocation",
            It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Allocate_OversizedIdempotencyKey_IsRejected()
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => h.Service.AllocateAsync(OpsIds.Decision, new string('k', 257), CancellationToken.None));

        Assert.Equal("The Idempotency-Key cannot exceed 256 characters.", ex.Message);
        Assert.Equal("VALIDATION_FAILED", ex.Code);
        Assert.Empty(h.Db.Allocations);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task CreateInventory_NegativeQuantityOrThreshold_IsRejected(decimal quantity, decimal threshold)
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateInventoryAsync(
            new CreateInventoryInput(OpsIds.FoodType, OpsIds.Org, quantity, threshold, null, null, null, false),
            CancellationToken.None));

        Assert.Equal("Quantity and reorder threshold cannot be negative.", ex.Message);
        Assert.Empty(h.Db.Inventories);
    }

    [Fact]
    public async Task UpdateInventory_NegativeQuantity_IsRejected()
    {
        var h = OpsHarness.Create();
        h.Db.Inventories.Add(OpsSeed.Inventory(10));
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.UpdateInventoryAsync(
            OpsIds.Inventory, new UpdateInventoryInput(-2, 1, null, 6.9, 79.8, false), CancellationToken.None));

        Assert.Equal("Quantity and reorder threshold cannot be negative.", ex.Message);
        Assert.Equal(10, (await h.Db.Inventories.SingleAsync()).Available);
    }

    [Fact]
    public async Task UpdateInventory_QuantityBelowCommittedStock_IsRejected()
    {
        var h = OpsHarness.Create();
        var inventory = OpsSeed.Inventory(10);
        inventory.Reserved = 4;
        inventory.Allocated = 3;
        h.Db.Inventories.Add(inventory);
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.UpdateInventoryAsync(
            OpsIds.Inventory, new UpdateInventoryInput(6, 1, null, 6.9, 79.8, false), CancellationToken.None));

        Assert.Equal("INVENTORY_BELOW_COMMITTED", ex.Code);
        Assert.Equal(10, inventory.Quantity);
    }

    [Fact]
    public async Task UpdateShelter_CapacityBelowOccupancy_IsRejected()
    {
        var h = OpsHarness.Create();
        h.Db.Shelters.Add(OpsSeed.Shelter(capacity: 10, occupancy: 7));
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.UpdateShelterAsync(
            OpsIds.Shelter,
            new UpdateShelterInput("Community shelter", 5, "[]", "[]", "[]", 6.9, 79.8),
            CancellationToken.None));

        Assert.Equal("SHELTER_CAPACITY_BELOW_OCCUPANCY", ex.Code);
        Assert.Equal(10, (await h.Db.Shelters.SingleAsync()).Capacity);
    }

    [Fact]
    public async Task CreateShelter_CapacityBelowOne_IsRejected()
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateShelterAsync(
            new CreateShelterInput("Shelter", 0, 0, "[]", "[]", "[]", 6.9, 79.8, OpsIds.Org),
            CancellationToken.None));

        Assert.Equal("Capacity must be at least one.", ex.Message);
        Assert.Empty(h.Db.Shelters);
    }

    [Theory]
    [InlineData(91d, 0d)]
    [InlineData(0d, -181d)]
    public async Task CreateShelter_OutOfRangeCoordinates_AreRejected(double lat, double lng)
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateShelterAsync(
            new CreateShelterInput("Shelter", 10, 0, "[]", "[]", "[]", lat, lng, OpsIds.Org),
            CancellationToken.None));

        Assert.Equal("Coordinates must be finite and within latitude/longitude bounds.", ex.Message);
        Assert.Empty(h.Db.Shelters);
    }

    [Theory]
    [InlineData(91d, 0d)]
    [InlineData(0d, 181d)]
    public async Task CreateInventory_OutOfRangeCoordinates_AreRejected(double lat, double lng)
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateInventoryAsync(
            new CreateInventoryInput(OpsIds.FoodType, OpsIds.Org, 5, 1, null, lat, lng, false),
            CancellationToken.None));

        Assert.Equal("Coordinates must be finite and within latitude/longitude bounds.", ex.Message);
        Assert.Empty(h.Db.Inventories);
    }

    [Theory]
    [InlineData(91d, 0d)]
    [InlineData(0d, -181d)]
    public async Task GetDistance_OutOfRangeCoordinates_AreRejected(double lat, double lng)
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => h.Service.GetDistanceAsync(lat, lng, 6.9, 79.8, CancellationToken.None));

        Assert.Equal("Coordinates must be finite and within latitude/longitude bounds.", ex.Message);
        h.Geo.Verify(g => g.GetDistanceAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AddAvailability_EndAtOrBeforeStart_IsRejected(bool equal)
    {
        var h = OpsHarness.Create();
        h.Db.Volunteers.Add(new Volunteer { Id = OpsIds.Volunteer, UserId = OpsIds.User, Active = true });
        await h.Db.SaveChangesAsync();
        var start = DateTime.UtcNow.AddDays(1);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.AddAvailabilityAsync(
            new AddAvailabilityInput(start, equal ? start : start.AddHours(-1)), CancellationToken.None));

        Assert.Equal("The availability window must have an end after its start.", ex.Message);
        Assert.Empty(h.Db.VolunteerAvailabilities);
    }

    [Fact]
    public async Task AddAvailability_WindowInThePast_IsRejected()
    {
        var h = OpsHarness.Create();
        h.Db.Volunteers.Add(new Volunteer { Id = OpsIds.Volunteer, UserId = OpsIds.User, Active = true });
        await h.Db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.AddAvailabilityAsync(
            new AddAvailabilityInput(DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1)),
            CancellationToken.None));

        Assert.Equal("Availability cannot start in the past.", ex.Message);
    }

    [Fact]
    public async Task SaveVolunteerProfile_TwiceForSameUser_KeepsExactlyOneProfile()
    {
        var h = OpsHarness.Create();

        var first = await h.Service.SaveVolunteerProfileAsync(
            new SaveVolunteerProfileInput("[\"medical\"]", "[\"en\"]", 6.9, 79.8, true), CancellationToken.None);
        var second = await h.Service.SaveVolunteerProfileAsync(
            new SaveVolunteerProfileInput("[\"driving\"]", "[\"si\"]", 7.0, 80.0, false), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(await h.Db.Volunteers.ToListAsync());
        Assert.Equal("[\"driving\"]", (await h.Db.Volunteers.SingleAsync()).SkillsJson);
        Assert.Equal("Pending", second.VerificationStatus);
    }

    [Fact]
    public async Task SaveVolunteerProfile_MalformedJsonArrays_AreRejected()
    {
        var h = OpsHarness.Create();

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.SaveVolunteerProfileAsync(
            new SaveVolunteerProfileInput("medical", "[\"en\"]", null, null, true), CancellationToken.None));

        Assert.Equal("Skills and languages must be JSON arrays of strings.", ex.Message);
        Assert.Empty(h.Db.Volunteers);
    }

    [Fact]
    public async Task CreateResourceType_BlankOrOversizedName_IsRejected()
    {
        var h = OpsHarness.Create();

        var blank = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateResourceTypeAsync(
            new CreateResourceTypeInput("   ", "packs", null), CancellationToken.None));
        Assert.Contains("name (max 120 characters) is required", blank.Message);

        var longName = await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateResourceTypeAsync(
            new CreateResourceTypeInput(new string('x', 121), "packs", null), CancellationToken.None));
        Assert.Contains("name (max 120 characters) is required", longName.Message);
        Assert.Empty(h.Db.ResourceTypes);
    }
}
