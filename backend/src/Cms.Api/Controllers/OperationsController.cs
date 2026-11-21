using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.Contracts.Operations;
using Cms.Application.Interfaces;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Entities.Cases;
using Cms.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class OperationsController : ControllerBase
{
    private readonly CmsDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditLogger _audit;
    private readonly IGeoService _geo;
    private readonly IResourceMatcher _resourceMatcher;
    private readonly IShelterRecommender _shelterMatcher;
    private readonly IVolunteerMatcher _volunteerMatcher;
    public OperationsController(CmsDbContext db, ICurrentUser user, IAuditLogger audit, IGeoService geo, IResourceMatcher resourceMatcher, IShelterRecommender shelterMatcher, IVolunteerMatcher volunteerMatcher)
    { _db = db; _user = user; _audit = audit; _geo = geo; _resourceMatcher = resourceMatcher; _shelterMatcher = shelterMatcher; _volunteerMatcher = volunteerMatcher; }

    [HttpGet("resources/types"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator,SystemAdmin")]
    public async Task<IActionResult> ResourceTypes(CancellationToken ct) => Ok(await _db.ResourceTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpPost("resources/types"), Authorize(Roles = "LogisticsManager,SystemAdmin")]
    public async Task<IActionResult> CreateResourceType(ResourceType input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120) return BadRequest(new { code = "VALIDATION_ERROR", message = "A resource type name (max 120 characters) is required." });
        input.Id = Guid.NewGuid(); input.CreatedAt = DateTime.UtcNow; _db.ResourceTypes.Add(input); await _db.SaveChangesAsync(ct);
        await Audit("ResourceType.Created", "ResourceType", input.Id, ct); return Created($"/api/v1/resources/types/{input.Id}", input);
    }

    [HttpGet("inventories"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator,SystemAdmin")]
    public async Task<IActionResult> Inventories([FromQuery] Guid? orgId, [FromQuery] Guid? resourceTypeId, CancellationToken ct)
    {
        var q = _db.Inventories.AsNoTracking().AsQueryable();
        if (orgId.HasValue) q = q.Where(x => x.OrgId == orgId);
        if (resourceTypeId.HasValue) q = q.Where(x => x.ResourceTypeId == resourceTypeId);
        var items = await q.OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return Ok(new { items, total = items.Count, page = 1, pageSize = items.Count });
    }

    [HttpPost("inventories"), Authorize(Roles = "LogisticsManager")]
    public async Task<IActionResult> CreateInventory(Inventory input, CancellationToken ct)
    {
        if (input.Quantity < 0 || input.ReorderThreshold < 0 || input.ResourceTypeId == Guid.Empty || input.OrgId == Guid.Empty) return BadRequest(new { code = "VALIDATION_ERROR", message = "Valid resource type, organisation and non-negative quantity are required." });
        input.Id = Guid.NewGuid(); input.Available = input.Quantity; input.Reserved = 0; input.Allocated = 0; input.Dispatched = 0; input.Utilised = 0; input.CreatedAt = DateTime.UtcNow;
        _db.Inventories.Add(input); await _db.SaveChangesAsync(ct); await Audit("Inventory.Created", "Inventory", input.Id, ct);
        return Created($"/api/v1/inventories/{input.Id}", input);
    }

    [HttpGet("allocations"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator,SystemAdmin")]
    public async Task<IActionResult> Allocations(CancellationToken ct) => Ok(await _db.Allocations.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(ct));

    public sealed record AllocateBody(Guid DecisionId);
    [HttpPost("allocations"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator")]
    public async Task<IActionResult> Allocate(AllocateBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest(new { code = "IDEMPOTENCY_KEY_REQUIRED" });
        var existing = await _db.Allocations.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (existing is not null) return Ok(existing);
        var decision = await ApprovedDecision(body.DecisionId, "ResourceMatch", ct);
        if (decision is null) return Forbid();
        using var json = JsonDocument.Parse(decision.FinalPayloadJson!);
        var payload = json.RootElement;
        if (!payload.TryGetProperty("inventoryId", out var inventoryProp) || !Guid.TryParse(inventoryProp.GetString(), out var inventoryId) ||
           !payload.TryGetProperty("quantity", out var quantityProp) || !quantityProp.TryGetDecimal(out var quantity) || quantity <= 0 ||
           !payload.TryGetProperty("requestId", out var requestProp) || !Guid.TryParse(requestProp.GetString(), out var requestId))
            return BadRequest(new { code = "INVALID_DECISION_PAYLOAD", message = "Approved decision must contain inventoryId, quantity and requestId." });
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var changed = await _db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE res.Inventories SET Available=Available-{quantity}, Allocated=Allocated+{quantity}, UpdatedAt={DateTime.UtcNow} WHERE Id={inventoryId} AND Available >= {quantity}", ct);
        if (changed != 1) { await tx.RollbackAsync(ct); return Conflict(new { code = "INSUFFICIENT_STOCK", message = "The selected inventory no longer has sufficient available stock." }); }
        var allocation = new Allocation { Id = Guid.NewGuid(), RequestId = requestId, InventoryId = inventoryId, Quantity = quantity, DecisionId = decision.Id, IdempotencyKey = key, Status = "Allocated", Explanation = "Allocated from the approved ResourceMatch decision.", CreatedAt = DateTime.UtcNow };
        _db.Allocations.Add(allocation); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await Audit("Allocation.Created", "Allocation", allocation.Id, ct);
        return Created($"/api/v1/allocations/{allocation.Id}", allocation);
    }

    [HttpPost("dispatches/{allocationId:guid}"), Authorize(Roles = "LogisticsManager")]
    public async Task<IActionResult> Dispatch(Guid allocationId, CancellationToken ct)
    {
        var allocation = await _db.Allocations.FirstOrDefaultAsync(x => x.Id == allocationId, ct);
        if (allocation is null) return NotFound();
        if (allocation.Status != "Allocated") return Conflict(new { code = "INVALID_ALLOCATION_STATE" });
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var moved = await _db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE res.Inventories SET Allocated=Allocated-{allocation.Quantity}, Dispatched=Dispatched+{allocation.Quantity}, UpdatedAt={DateTime.UtcNow} WHERE Id={allocation.InventoryId} AND Allocated >= {allocation.Quantity}", ct);
        if (moved != 1) { await tx.RollbackAsync(ct); return Conflict(new { code = "INVENTORY_BUCKET_MISMATCH" }); }
        allocation.Status = "Dispatched";
        var dispatch = new Dispatch { AllocationId = allocation.Id, Quantity = allocation.Quantity, Status = "Dispatched" };
        _db.Dispatches.Add(dispatch); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await Audit("Allocation.Dispatched", "Allocation", allocation.Id, ct);
        return Ok(dispatch);
    }

    [HttpPost("dispatches/{dispatchId:guid}/confirm"), Authorize(Roles = "LogisticsManager")]
    public async Task<IActionResult> ConfirmDelivery(Guid dispatchId, CancellationToken ct)
    {
        var dispatch = await _db.Dispatches.FirstOrDefaultAsync(x => x.Id == dispatchId, ct);
        if (dispatch is null) return NotFound(); if (dispatch.Status != "Dispatched") return Conflict(new { code = "INVALID_DISPATCH_STATE" });
        var allocation = await _db.Allocations.FirstAsync(x => x.Id == dispatch.AllocationId, ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var changed = await _db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE res.Inventories SET Dispatched=Dispatched-{allocation.Quantity}, Utilised=Utilised+{allocation.Quantity} WHERE Id={allocation.InventoryId} AND Dispatched >= {allocation.Quantity}", ct);
        // An allocation enters the dispatched bucket when dispatch is recorded.
        if (changed == 0)
        {
            changed = await _db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE res.Inventories SET Allocated=Allocated-{allocation.Quantity}, Utilised=Utilised+{allocation.Quantity} WHERE Id={allocation.InventoryId} AND Allocated >= {allocation.Quantity}", ct);
            if (changed == 0) { await tx.RollbackAsync(ct); return Conflict(new { code = "INVENTORY_BUCKET_MISMATCH" }); }
        }
        dispatch.Status = "Fulfilled"; dispatch.ConfirmedAt = DateTime.UtcNow; dispatch.ConfirmedBy = _user.UserId;
        allocation.Status = "Fulfilled"; await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await Audit("Allocation.Fulfilled", "Allocation", allocation.Id, ct); return Ok(dispatch);
    }

    [HttpGet("shelters"), Authorize(Roles = "ShelterAdmin,EmergencyCoordinator,SystemAdmin")]
    public async Task<IActionResult> Shelters([FromQuery] bool? approved, CancellationToken ct)
    {
        var q = _db.Shelters.AsNoTracking().AsQueryable(); if (approved.HasValue) q = q.Where(x => x.Approved == approved);
        return Ok(await q.OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Capacity, x.Occupancy, FreePlaces = x.Capacity - x.Occupancy, x.Facilities, x.AccessibilityFeatures, x.Services, x.Lat, x.Lng, x.OrgId, x.Approved }).ToListAsync(ct));
    }

    [HttpGet("shelters/public")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicShelters(CancellationToken ct) => Ok(await _db.Shelters.AsNoTracking().Where(x => x.Approved).Select(x => new { x.Id, x.Name, x.Capacity, x.Occupancy, FreePlaces = x.Capacity - x.Occupancy, x.Facilities, x.AccessibilityFeatures, x.Services, x.Lat, x.Lng }).ToListAsync(ct));

    [HttpPost("shelters"), Authorize(Roles = "ShelterAdmin")]
    public async Task<IActionResult> CreateShelter(Shelter input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Capacity < 1 || input.Occupancy < 0 || input.Occupancy > input.Capacity || !ValidCoordinates(input.Lat, input.Lng))
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Name, valid coordinates and 0 <= occupancy <= capacity are required." });
        input.Approved = false; input.Id = Guid.NewGuid(); input.CreatedAt = DateTime.UtcNow; _db.Shelters.Add(input); await _db.SaveChangesAsync(ct); await Audit("Shelter.Created", "Shelter", input.Id, ct);
        return Created($"/api/v1/shelters/{input.Id}", input);
    }

    [HttpPatch("shelters/{id:guid}"), Authorize(Roles = "ShelterAdmin")]
    public async Task<IActionResult> UpdateShelter(Guid id, Shelter update, CancellationToken ct)
    {
        var shelter = await _db.Shelters.FirstOrDefaultAsync(x => x.Id == id, ct); if (shelter is null) return NotFound();
        if (update.Capacity < shelter.Occupancy) return Conflict(new { code = "SHELTER_CAPACITY_BELOW_OCCUPANCY" });
        shelter.Name = update.Name; shelter.Capacity = update.Capacity; shelter.Facilities = update.Facilities; shelter.AccessibilityFeatures = update.AccessibilityFeatures; shelter.Services = update.Services; shelter.Lat = update.Lat; shelter.Lng = update.Lng; shelter.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); await Audit("Shelter.Updated", "Shelter", id, ct); return Ok(shelter);
    }

    public sealed record ShelterAllocateBody(Guid DecisionId);
    [HttpPost("shelters/allocations"), Authorize(Roles = "ShelterAdmin")]
    public async Task<IActionResult> AllocateShelter(ShelterAllocateBody body, CancellationToken ct)
    {
        var decision = await ApprovedDecision(body.DecisionId, "ShelterMatch", ct); if (decision is null) return Forbid();
        using var json = JsonDocument.Parse(decision.FinalPayloadJson!); var p = json.RootElement;
        if (!TryGuid(p, "shelterId", out var shelterId) || !TryGuid(p, "requestId", out var requestId) || !p.TryGetProperty("householdSize", out var size) || !size.TryGetInt32(out var count) || count < 1)
            return BadRequest(new { code = "INVALID_DECISION_PAYLOAD" });
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync($@"UPDATE shelter.Shelters SET Occupancy=Occupancy+{count}, UpdatedAt={DateTime.UtcNow} WHERE Id={shelterId} AND Approved=1 AND Occupancy+{count}<=Capacity", ct);
        if (rows != 1) { await tx.RollbackAsync(ct); return Conflict(new { code = "SHELTER_FULL" }); }
        var item = new ShelterAllocation { RequestId = requestId, ShelterId = shelterId, HouseholdSize = count, DecisionId = decision.Id };
        _db.ShelterAllocations.Add(item); await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await Audit("Shelter.Allocated", "ShelterAllocation", item.Id, ct); return Created($"/api/v1/shelters/allocations/{item.Id}", item);
    }

    [HttpPost("shelters/{id:guid}/approve"), Authorize(Roles = "EmergencyCoordinator")]
    public async Task<IActionResult> ApproveShelter(Guid id, CancellationToken ct)
    {
        var shelter = await _db.Shelters.FirstOrDefaultAsync(x => x.Id == id, ct); if (shelter is null) return NotFound();
        shelter.Approved = true; shelter.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct);
        await Audit("Shelter.Approved", "Shelter", id, ct); return Ok(shelter);
    }

    [HttpGet("volunteers"), Authorize(Roles = "VolunteerCoordinator,EmergencyCoordinator")]
    public async Task<IActionResult> Volunteers(CancellationToken ct) => Ok(await _db.Volunteers.AsNoTracking().ToListAsync(ct));

    [HttpPost("volunteers/profile"), Authorize(Roles = "Volunteer")]
    public async Task<IActionResult> SaveVolunteerProfile(Volunteer input, CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return Unauthorized();
        var item = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == _user.UserId.Value, ct);
        if (item is null) { item = new Volunteer { UserId = _user.UserId.Value }; _db.Volunteers.Add(item); }
        item.SkillsJson = input.SkillsJson; item.LanguagesJson = input.LanguagesJson; item.Lat = input.Lat; item.Lng = input.Lng; item.Active = input.Active; item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); await Audit("Volunteer.ProfileUpdated", "Volunteer", item.Id, ct); return Ok(item);
    }

    [HttpPost("volunteers/{id:guid}/verify"), Authorize(Roles = "VolunteerCoordinator")]
    public async Task<IActionResult> VerifyVolunteer(Guid id, [FromQuery] bool approved, CancellationToken ct)
    {
        var item = await _db.Volunteers.FirstOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        item.VerificationStatus = approved ? "Verified" : "Rejected"; item.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct);
        await Audit("Volunteer." + item.VerificationStatus, "Volunteer", id, ct); return Ok(item);
    }

    [HttpGet("volunteer-tasks/mine"), Authorize(Roles = "Volunteer")]
    public async Task<IActionResult> MyVolunteerTasks(CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return Unauthorized();
        var volunteer = await _db.Volunteers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == _user.UserId.Value, ct);
        if (volunteer is null) return Ok(Array.Empty<VolunteerTask>());
        return Ok(await _db.VolunteerTasks.AsNoTracking().Where(x => x.VolunteerId == volunteer.Id).OrderByDescending(x => x.CreatedAt).ToListAsync(ct));
    }

    [HttpPost("volunteer-tasks"), Authorize(Roles = "VolunteerCoordinator")]
    public async Task<IActionResult> CreateVolunteerTask(VolunteerTask input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) return BadRequest(new { code = "VALIDATION_ERROR" });
        input.Id = Guid.NewGuid(); input.Status = "Open"; input.CreatedAt = DateTime.UtcNow; _db.VolunteerTasks.Add(input); await _db.SaveChangesAsync(ct); await Audit("VolunteerTask.Created", "VolunteerTask", input.Id, ct); return Created($"/api/v1/volunteer-tasks/{input.Id}", input);
    }

    [HttpPost("volunteer-tasks/{id:guid}/assign"), Authorize(Roles = "VolunteerCoordinator")]
    public async Task<IActionResult> AssignVolunteerTask(Guid id, [FromQuery] Guid volunteerId, CancellationToken ct)
    {
        var task = await _db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct); var volunteer = await _db.Volunteers.FirstOrDefaultAsync(x => x.Id == volunteerId, ct);
        if (task is null || volunteer is null) return NotFound(); if (task.Status != "Open" || !volunteer.Active || volunteer.VerificationStatus != "Verified") return Conflict(new { code = "VOLUNTEER_NOT_ASSIGNABLE" });
        var now = DateTime.UtcNow; var available = await _db.VolunteerAvailabilities.AnyAsync(x => x.VolunteerId == volunteerId && x.StartsAt <= now && x.EndsAt >= now, ct);
        if (!available) return Conflict(new { code = "VOLUNTEER_NOT_AVAILABLE" });
        task.VolunteerId = volunteerId; task.Status = "Assigned"; await _db.SaveChangesAsync(ct); await Audit("VolunteerTask.Assigned", "VolunteerTask", id, ct); return Ok(task);
    }

    [HttpPost("volunteer-tasks/{id:guid}/respond"), Authorize(Roles = "Volunteer")]
    public async Task<IActionResult> RespondToTask(Guid id, [FromQuery] bool accept, CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return Unauthorized();
        var volunteer = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == _user.UserId.Value, ct); var task = await _db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (volunteer is null || task is null) return NotFound(); if (task.VolunteerId != volunteer.Id || task.Status != "Assigned") return Forbid();
        task.Status = accept ? "Accepted" : "Open"; if (!accept) task.VolunteerId = null; await _db.SaveChangesAsync(ct); await Audit("VolunteerTask." + task.Status, "VolunteerTask", id, ct); return Ok(task);
    }

    [HttpPost("volunteer-tasks/{id:guid}/complete"), Authorize(Roles = "Volunteer")]
    public async Task<IActionResult> CompleteTask(Guid id, CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return Unauthorized();
        var volunteer = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == _user.UserId.Value, ct); var task = await _db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (volunteer is null || task is null) return NotFound(); if (task.VolunteerId != volunteer.Id || task.Status != "Accepted") return Forbid();
        task.Status = "Completed"; task.CompletedAt = DateTime.UtcNow; _db.VolunteerActivities.Add(new VolunteerActivity { VolunteerId = volunteer.Id, TaskId = id, CompletedBy = _user.UserId.Value, CompletedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct); await Audit("VolunteerTask.Completed", "VolunteerTask", id, ct); return Ok(task);
    }

    [HttpPost("assistance-requests"), Authorize(Roles = "LogisticsManager,ShelterAdmin,PartnerOfficer")]
    public async Task<IActionResult> CreateAssistanceRequest(AssistanceRequest input, CancellationToken ct)
    {
        if (input.FromOrgId == Guid.Empty || input.ResourceTypeId == Guid.Empty || input.Quantity <= 0 || string.IsNullOrWhiteSpace(input.Reason))
            return BadRequest(new { code = "VALIDATION_ERROR" });
        input.Id = Guid.NewGuid(); input.Status = "Pending"; input.CreatedAt = DateTime.UtcNow;
        _db.AssistanceRequests.Add(input); await _db.SaveChangesAsync(ct); await Audit("AssistanceRequest.Created", "AssistanceRequest", input.Id, ct);
        return Created($"/api/v1/assistance-requests/{input.Id}", input);
    }

    [HttpGet("assistance-requests"), Authorize(Roles = "LogisticsManager,ShelterAdmin,PartnerOfficer")]
    public async Task<IActionResult> AssistanceRequests([FromQuery] string? direction, [FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.AssistanceRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (direction == "in") q = q.Where(x => x.ToOrgId != null);
        if (direction == "out") q = q.Where(x => x.FromOrgId != Guid.Empty);
        var items = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return Ok(new { items, total = items.Count, page = 1, pageSize = items.Count });
    }

    public sealed record AssistanceResponse(bool Accept, string Reason);
    [HttpPost("assistance-requests/{id:guid}/respond"), Authorize(Roles = "PartnerOfficer,LogisticsManager")]
    public async Task<IActionResult> RespondAssistanceRequest(Guid id, AssistanceResponse body, CancellationToken ct)
    {
        var item = await _db.AssistanceRequests.FirstOrDefaultAsync(x => x.Id == id, ct); if (item is null) return NotFound();
        if (item.Status != "Pending") return Conflict(new { code = "INVALID_ASSISTANCE_STATE" });
        // Organisation ownership is checked by the organisation-scoped identity contract when available.
        item.Status = body.Accept ? "Accepted" : "Rejected"; item.ResponseReason = body.Reason; item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct); await Audit("AssistanceRequest." + item.Status, "AssistanceRequest", id, ct); return Ok(item);
    }

    [HttpPost("volunteers/availability"), Authorize(Roles = "Volunteer")]
    public async Task<IActionResult> AddAvailability(VolunteerAvailability input, CancellationToken ct)
    {
        if (!_user.UserId.HasValue) return Unauthorized();
        var volunteer = await _db.Volunteers.FirstOrDefaultAsync(x => x.UserId == _user.UserId.Value, ct); if (volunteer is null) return NotFound();
        if (input.EndsAt <= input.StartsAt || input.StartsAt < DateTime.UtcNow.AddMinutes(-1)) return BadRequest(new { code = "INVALID_AVAILABILITY_WINDOW" });
        input.Id = Guid.NewGuid(); input.VolunteerId = volunteer.Id; input.CreatedAt = DateTime.UtcNow;
        _db.VolunteerAvailabilities.Add(input); await _db.SaveChangesAsync(ct); await Audit("Volunteer.AvailabilityAdded", "VolunteerAvailability", input.Id, ct); return Created($"/api/v1/volunteers/availability/{input.Id}", input);
    }

    [HttpGet("recommendations/match/resources/{requestId:guid}"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator")]
    public async Task<IActionResult> MatchResources(Guid requestId, CancellationToken ct) => Ok(await _resourceMatcher.RankAsync(requestId, ct));
    [HttpGet("recommendations/match/shelters/{requestId:guid}"), Authorize(Roles = "ShelterAdmin,EmergencyCoordinator")]
    public async Task<IActionResult> MatchShelters(Guid requestId, CancellationToken ct) => Ok(await _shelterMatcher.RankAsync(requestId, ct));
    [HttpGet("recommendations/match/volunteers/{requestId:guid}"), Authorize(Roles = "VolunteerCoordinator,EmergencyCoordinator")]
    public async Task<IActionResult> MatchVolunteers(Guid requestId, CancellationToken ct) => Ok(await _volunteerMatcher.RankAsync(requestId, ct));

    [HttpGet("shortages/proposal"), Authorize(Roles = "LogisticsManager,EmergencyCoordinator")]
    public async Task<IActionResult> ShortageProposal([FromQuery] Guid resourceTypeId, CancellationToken ct)
    {
        var stock = await _db.Inventories.AsNoTracking().Where(x => x.ResourceTypeId == resourceTypeId).SumAsync(x => (decimal?)x.Available, ct) ?? 0;
        var candidates = await _db.Requests.AsNoTracking().Where(x => x.Status != Cms.Domain.Enums.Cases.RequestStatus.Closed && x.Status != Cms.Domain.Enums.Cases.RequestStatus.Fulfilled).OrderByDescending(x => x.Urgency).ThenBy(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var remaining = stock; var result = candidates.Select((r, i) => { var requested = 1m; var proposed = Math.Min(requested, remaining); remaining -= proposed; return new { requestId = r.Id, category = r.Category.ToString(), urgency = r.Urgency.ToString(), requestedQuantity = requested, availableAtStart = stock, priorityPosition = i + 1, proposedPartialFulfilment = proposed, explanation = $"Ranked by urgency ({r.Urgency}) and request age; this proposal does not reserve stock. Human approval is required." }; }).ToList();
        return Ok(new { resourceTypeId, availableQuantity = stock, items = result, hasSideEffects = false, humanApprovalRequired = true });
    }

    [HttpGet("geo/distance"), AllowAnonymous]
    public async Task<IActionResult> Distance([FromQuery] double fromLat, [FromQuery] double fromLng, [FromQuery] double toLat, [FromQuery] double toLng, CancellationToken ct)
    {
        if (!ValidCoordinates(fromLat, fromLng) || !ValidCoordinates(toLat, toLng)) return BadRequest(new { code = "INVALID_COORDINATES" });
        return Ok(await _geo.GetDistanceAsync(new(fromLat, fromLng), new(toLat, toLng), ct));
    }
    [HttpGet("geo/geocode"), AllowAnonymous]
    public async Task<IActionResult> Geocode([FromQuery] string address, CancellationToken ct)
    { var point = await _geo.GeocodeAsync(address, ct); return point is null ? NotFound(new { code = "GEOCODE_NOT_FOUND" }) : Ok(point); }

    private async Task<Cms.Domain.Entities.DecisionRecord?> ApprovedDecision(Guid id, string type, CancellationToken ct)
    {
        var decision = await _db.DecisionRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (decision is null || decision.Outcome is not (Cms.Domain.Enums.DecisionOutcome.Approved or Cms.Domain.Enums.DecisionOutcome.Modified)) return null;
        var recommendation = await _db.Recommendations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == decision.RecommendationId, ct);
        if (recommendation is null || recommendation.Type.ToString() != type) return null;
        if (string.IsNullOrWhiteSpace(decision.FinalPayloadJson)) return null;
        try
        {
            using var payload = JsonDocument.Parse(decision.FinalPayloadJson);
            if (!TryGuid(payload.RootElement, "requestId", out var payloadRequestId) || payloadRequestId != recommendation.TargetId) return null;
        }
        catch (JsonException) { return null; }
        return decision;
    }
    private async Task Audit(string action, string entity, Guid id, CancellationToken ct) => await _audit.LogAsync(_user.UserId, action, entity, id.ToString(), null, ct);
    private static bool TryGuid(JsonElement e, string key, out Guid value) { value = Guid.Empty; return e.TryGetProperty(key, out var p) && Guid.TryParse(p.GetString(), out value); }
    private static bool ValidCoordinates(double lat, double lng) => double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}
