using Cms.Application.Operations;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

/// <summary>Thin HTTP surface for the Member 4 operations service.</summary>
[ApiController]
[Route("api/v1")]
public sealed class OperationsController(IOperationsService operations) : ControllerBase
{
    public sealed record DecisionBody(Guid DecisionId);
    public sealed record DispatchBody(Guid DecisionId, Guid? AllocationId = null);
    public sealed record DeliveryBody(Guid DecisionId, Guid? DispatchId = null);
    public sealed record ShelterAllocateBody(Guid DecisionId, Guid? RequestId = null, int? HouseholdSize = null);
    public sealed record VerificationBody(bool Approved, string Reason);
    public sealed record TaskResponse(bool Accept, string? Reason = null);
    public sealed record AssistanceResponse(bool Accept, string Reason);

    private const string StaffRoles =
        RoleNames.LogisticsManager + "," + RoleNames.EmergencyCoordinator + "," + RoleNames.SystemAdmin;

    // -----------------------------------------------------------------------
    // M5 resources.
    // -----------------------------------------------------------------------
    [HttpGet("resources"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Resources(CancellationToken ct) => Ok(await operations.GetResourceTypesAsync(ct));

    [HttpGet("resources/types"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> ResourceTypes(CancellationToken ct) => Ok(await operations.GetResourceTypesAsync(ct));

    [HttpPost("resources/types"), Authorize(Roles = RoleNames.LogisticsManager + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> CreateResourceType(CreateResourceTypeInput input, CancellationToken ct)
    {
        var row = await operations.CreateResourceTypeAsync(input, ct);
        return Created($"/api/v1/resources/types/{row.Id}", row);
    }

    [HttpGet("inventories"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Inventories(
        [FromQuery] Guid? orgId, [FromQuery] Guid? resourceTypeId, [FromQuery] Guid? areaId,
        [FromQuery] bool? lowStock, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetInventoriesAsync(orgId, resourceTypeId, areaId, lowStock, page, pageSize, ct));

    [HttpPost("inventories"), Authorize(Roles = RoleNames.LogisticsManager + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> CreateInventory(CreateInventoryInput input, CancellationToken ct)
    {
        var row = await operations.CreateInventoryAsync(input, ct);
        return Created($"/api/v1/inventories/{row.Id}", row);
    }

    [HttpPatch("inventories/{id:guid}"), Authorize(Roles = RoleNames.LogisticsManager + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> UpdateInventory(Guid id, UpdateInventoryInput input, CancellationToken ct) =>
        Ok(await operations.UpdateInventoryAsync(id, input, ct));

    [HttpGet("allocations"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Allocations(
        [FromQuery] Guid? requestId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetAllocationsAsync(requestId, status, page, pageSize, ct));

    [HttpPost("allocations"), Authorize(Roles = RoleNames.LogisticsManager + "," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> Allocate(
        DecisionBody body, [FromHeader(Name = "Idempotency-Key")] string? key, CancellationToken ct)
    {
        var row = await operations.AllocateAsync(body.DecisionId, key ?? string.Empty, ct);
        return Created($"/api/v1/allocations/{row.Id}", row);
    }

    [HttpGet("dispatches"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Dispatches(
        [FromQuery] Guid? allocationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetDispatchesAsync(allocationId, page, pageSize, ct));

    [HttpPost("dispatches"), Authorize(Roles = RoleNames.LogisticsManager)]
    public async Task<IActionResult> Dispatch(DispatchBody body, CancellationToken ct) =>
        Ok(await operations.DispatchAsync(body.DecisionId, body.AllocationId, ct));

    [HttpPost("dispatches/{allocationId:guid}"), Authorize(Roles = RoleNames.LogisticsManager)]
    public async Task<IActionResult> DispatchByAllocation(Guid allocationId, DecisionBody body, CancellationToken ct) =>
        Ok(await operations.DispatchAsync(body.DecisionId, allocationId, ct));

    [HttpPost("dispatches/confirm"), Authorize(Roles = RoleNames.LogisticsManager)]
    public async Task<IActionResult> Confirm(DeliveryBody body, CancellationToken ct) =>
        Ok(await operations.ConfirmDeliveryAsync(body.DecisionId, body.DispatchId, ct));

    [HttpPost("dispatches/{dispatchId:guid}/confirm"), Authorize(Roles = RoleNames.LogisticsManager)]
    public async Task<IActionResult> ConfirmByDispatch(Guid dispatchId, DecisionBody body, CancellationToken ct) =>
        Ok(await operations.ConfirmDeliveryAsync(body.DecisionId, dispatchId, ct));

    // -----------------------------------------------------------------------
    // M6 shelters.
    // -----------------------------------------------------------------------
    [HttpGet("shelters"), Authorize(Roles = "ShelterAdmin," + RoleNames.EmergencyCoordinator + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> Shelters(
        [FromQuery] bool? approved, [FromQuery] Guid? orgId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetSheltersAsync(approved, orgId, page, pageSize, ct));

    [HttpGet("shelters/public"), AllowAnonymous, EnableRateLimiting("public")]
    public async Task<IActionResult> PublicShelters(CancellationToken ct) => Ok(await operations.GetPublicSheltersAsync(ct));

    [HttpPost("shelters"), Authorize(Roles = "ShelterAdmin," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> CreateShelter(CreateShelterInput input, CancellationToken ct)
    {
        var row = await operations.CreateShelterAsync(input, ct);
        return Created($"/api/v1/shelters/{row.Id}", row);
    }

    [HttpPatch("shelters/{id:guid}"), Authorize(Roles = "ShelterAdmin," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> UpdateShelter(Guid id, UpdateShelterInput input, CancellationToken ct) =>
        Ok(await operations.UpdateShelterAsync(id, input, ct));

    [HttpPost("shelters/allocations"), Authorize(Roles = "ShelterAdmin," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> AllocateShelter(ShelterAllocateBody body, CancellationToken ct)
    {
        var row = await operations.AllocateShelterAsync(body.DecisionId, ct);
        return Created($"/api/v1/shelters/allocations/{row.Id}", row);
    }

    [HttpPost("shelters/{id:guid}/approve"), Authorize(Roles = RoleNames.EmergencyCoordinator + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> ApproveShelter(Guid id, CancellationToken ct) =>
        Ok(await operations.ApproveShelterAsync(id, ct));

    // -----------------------------------------------------------------------
    // M7 volunteers.
    // -----------------------------------------------------------------------
    [HttpGet("volunteers"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.EmergencyCoordinator + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> Volunteers(CancellationToken ct) => Ok(await operations.GetVolunteersAsync(ct));

    [HttpPost("volunteers/profile"), Authorize(Roles = RoleNames.Volunteer)]
    public async Task<IActionResult> SaveVolunteerProfile(SaveVolunteerProfileInput input, CancellationToken ct) =>
        Ok(await operations.SaveVolunteerProfileAsync(input, ct));

    [HttpPost("volunteers/{id:guid}/verify"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> VerifyVolunteer(Guid id, VerificationBody body, CancellationToken ct) =>
        Ok(await operations.VerifyVolunteerAsync(id, body.Approved, body.Reason, ct));

    [HttpPost("volunteers/availability"), Authorize(Roles = RoleNames.Volunteer)]
    public async Task<IActionResult> AddAvailability(AddAvailabilityInput input, CancellationToken ct)
    {
        var row = await operations.AddAvailabilityAsync(input, ct);
        return Created($"/api/v1/volunteers/availability/{row.Id}", row);
    }

    [HttpGet("volunteer-tasks/mine"), Authorize(Roles = RoleNames.Volunteer)]
    public async Task<IActionResult> MyVolunteerTasks(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetMyTasksAsync(page, pageSize, ct));

    [HttpGet("volunteer-tasks"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> VolunteerTasks(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetVolunteerTasksAsync(status, page, pageSize, ct));

    [HttpPost("volunteer-tasks"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> CreateVolunteerTask(CreateVolunteerTaskInput input, CancellationToken ct)
    {
        var row = await operations.CreateVolunteerTaskAsync(input, ct);
        return Created($"/api/v1/volunteer-tasks/{row.Id}", row);
    }

    [HttpPost("volunteer-tasks/{id:guid}/assign"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> AssignVolunteerTask(Guid id, [FromQuery] Guid volunteerId, CancellationToken ct) =>
        Ok(await operations.AssignVolunteerTaskAsync(id, volunteerId, ct));

    [HttpPost("volunteer-tasks/{id:guid}/respond"), Authorize(Roles = RoleNames.Volunteer)]
    public async Task<IActionResult> RespondToTask(Guid id, TaskResponse body, CancellationToken ct) =>
        Ok(await operations.RespondToTaskAsync(id, body.Accept, body.Reason, ct));

    [HttpPost("volunteer-tasks/{id:guid}/complete"), Authorize(Roles = RoleNames.Volunteer)]
    public async Task<IActionResult> CompleteTask(Guid id, CancellationToken ct) =>
        Ok(await operations.CompleteTaskAsync(id, ct));

    // -----------------------------------------------------------------------
    // M5 assistance + shortage.
    // -----------------------------------------------------------------------
    [HttpPost("assistance-requests"), Authorize(Roles = RoleNames.LogisticsManager + ",ShelterAdmin," + RoleNames.PartnerOfficer)]
    public async Task<IActionResult> CreateAssistanceRequest(CreateAssistanceRequestInput input, CancellationToken ct)
    {
        var row = await operations.CreateAssistanceRequestAsync(input, ct);
        return Created($"/api/v1/assistance-requests/{row.Id}", row);
    }

    [HttpGet("assistance-requests"), Authorize(Roles = RoleNames.LogisticsManager + ",ShelterAdmin," + RoleNames.PartnerOfficer)]
    public async Task<IActionResult> AssistanceRequests(
        [FromQuery] string? direction, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        Ok(await operations.GetAssistanceRequestsAsync(direction, status, page, pageSize, ct));

    [HttpPost("assistance-requests/{id:guid}/respond"), Authorize(Roles = RoleNames.PartnerOfficer + "," + RoleNames.LogisticsManager + "," + RoleNames.SystemAdmin)]
    public async Task<IActionResult> RespondAssistanceRequest(Guid id, AssistanceResponse body, CancellationToken ct) =>
        Ok(await operations.RespondAssistanceRequestAsync(id, body.Accept, body.Reason, ct));

    [HttpGet("shortages/proposal"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> ShortageProposal([FromQuery] Guid resourceTypeId, CancellationToken ct) =>
        Ok(await operations.GetShortageProposalAsync(resourceTypeId, ct));

    // -----------------------------------------------------------------------
    // Match outputs and recovery.
    // -----------------------------------------------------------------------
    [HttpGet("recommendations/match/resources/{requestId:guid}"), Authorize(Roles = RoleNames.LogisticsManager + "," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> MatchResources(Guid requestId, CancellationToken ct) =>
        Ok(await operations.RankResourcesAsync(requestId, ct));

    [HttpGet("recommendations/match/shelters/{requestId:guid}"), Authorize(Roles = "ShelterAdmin," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> MatchShelters(Guid requestId, CancellationToken ct) =>
        Ok(await operations.RankSheltersAsync(requestId, ct));

    [HttpGet("recommendations/match/volunteers/{requestId:guid}"), Authorize(Roles = "VolunteerCoordinator," + RoleNames.EmergencyCoordinator)]
    public async Task<IActionResult> MatchVolunteers(Guid requestId, CancellationToken ct) =>
        Ok(await operations.RankVolunteersAsync(requestId, ct));

    [HttpGet("operations/status-transitions"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> PendingTransitions(CancellationToken ct) =>
        Ok(await operations.GetPendingTransitionsAsync(ct));

    [HttpPost("operations/status-transitions/{id:guid}/retry"), Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> RetryTransition(Guid id, CancellationToken ct)
    {
        var row = await operations.RetryStatusTransitionAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    // -----------------------------------------------------------------------
    // M10 mapping proxy endpoints.
    // -----------------------------------------------------------------------
    [HttpGet("geo/distance"), AllowAnonymous]
    public async Task<IActionResult> Distance(
        [FromQuery] double fromLat, [FromQuery] double fromLng,
        [FromQuery] double toLat, [FromQuery] double toLng, CancellationToken ct) =>
        Ok(await operations.GetDistanceAsync(fromLat, fromLng, toLat, toLng, ct));

    [HttpGet("geo/geocode"), AllowAnonymous]
    public async Task<IActionResult> Geocode([FromQuery] string address, CancellationToken ct)
    {
        var point = await operations.GeocodeAsync(address, ct);
        return point is null ? NotFound(new { code = "GEOCODE_NOT_FOUND" }) : Ok(point);
    }
}
