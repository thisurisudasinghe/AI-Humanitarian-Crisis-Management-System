using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Cms.Application.Operations;
using Cms.Domain.Cases;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using DecisionDto = Cms.Application.Contracts.DecisionDto;
using DistanceResult = Cms.Application.Contracts.DistanceResult;
using GeoPoint = Cms.Application.Contracts.GeoPoint;
using IGeoService = Cms.Application.Contracts.IGeoService;
using MatchCandidate = Cms.Application.Contracts.MatchCandidate;

namespace Cms.Infrastructure.Operations;

/// <summary>
/// Member 4 application boundary. All business rules, transactions, side effects
/// and audit/outbox writes for M5/M6/M7/M10 live here; controllers only map HTTP.
/// </summary>
public sealed class OperationsService(
    CmsDbContext db,
    ICurrentUser user,
    IAuditLogger audit,
    IDecisionGate decisionGate,
    IRequestReader requestReader,
    IRequestStatusService requestStatus,
    IOutbox outbox,
    IGeoService geo,
    IResourceMatcher resourceMatcher,
    IShelterRecommender shelterRecommender,
    IVolunteerMatcher volunteerMatcher) : IOperationsService
{
    // Deterministic resource-match weights (sum = 1.00):
    //   type/category fit 0.25 | available quantity 0.20 | distance 0.15
    //   urgency fit       0.15 | expiry confidence     0.10 | accessibility 0.10
    //   org ownership     0.05
    // Shelter weights: availability 0.30 | suitability 0.25 | distance 0.25 |
    //                  services 0.10 | org ownership 0.10.
    // Volunteer weights: skill overlap 0.45 | availability fit 0.25 |
    //                   distance 0.20 | request context 0.10.
    private const int MaxPageSize = 200;

    // -----------------------------------------------------------------------
    // M5 resources.
    // -----------------------------------------------------------------------
    public async Task<IReadOnlyList<ResourceTypeDto>> GetResourceTypesAsync(CancellationToken ct)
    {
        var rows = await db.ResourceTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        return rows.Select(x => new ResourceTypeDto(x.Id, x.Name, x.Unit, x.Description)).ToArray();
    }

    public async Task<ResourceTypeDto> CreateResourceTypeAsync(CreateResourceTypeInput input, CancellationToken ct)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 120) throw Validation("A resource type name (max 120 characters) is required.");
        var unit = string.IsNullOrWhiteSpace(input.Unit) ? "units" : input.Unit.Trim();
        if (unit.Length > 40) throw Validation("The unit cannot exceed 40 characters.");
        if (input.Description is { Length: > 500 }) throw Validation("The description cannot exceed 500 characters.");
        if (await db.ResourceTypes.AsNoTracking().AnyAsync(x => x.Name == name, ct)) throw Conflict("RESOURCE_TYPE_EXISTS", "A resource type with this name already exists.");

        var row = new ResourceType { Id = Guid.NewGuid(), Name = name, Unit = unit, Description = input.Description?.Trim() };
        db.ResourceTypes.Add(row);
        await db.SaveChangesAsync(ct);
        await AuditAsync("ResourceType.Created", "ResourceType", row.Id, new { name, unit }, ct);
        return new ResourceTypeDto(row.Id, row.Name, row.Unit, row.Description);
    }

    public async Task<PagedResult<InventoryDto>> GetInventoriesAsync(
        Guid? orgId, Guid? resourceTypeId, Guid? areaId, bool? lowStock,
        int page, int pageSize, CancellationToken ct)
    {
        var q = db.Inventories.AsNoTracking().AsQueryable();
        if (orgId.HasValue) q = q.Where(x => x.OrgId == orgId.Value);
        if (resourceTypeId.HasValue) q = q.Where(x => x.ResourceTypeId == resourceTypeId.Value);
        if (lowStock == true) q = q.Where(x => x.Available <= x.ReorderThreshold);
        if (lowStock == false) q = q.Where(x => x.Available > x.ReorderThreshold);

        var scopedOrg = ScopedOrgId();
        if (scopedOrg.HasValue && !IsPrivileged()) q = q.Where(x => x.OrgId == scopedOrg.Value);

        if (areaId.HasValue)
        {
            // Inventories are stored per organisation; the operational-area filter is
            // resolved through requests allocated from that inventory, which keeps the
            // filter side-effect free and consistent with the fixed schema.
            var linked = db.Allocations.AsNoTracking()
                .Join(db.Requests.AsNoTracking(), a => a.RequestId, r => r.Id, (a, r) => new { a.InventoryId, r.AreaId })
                .Where(x => x.AreaId == areaId)
                .Select(x => x.InventoryId)
                .Distinct();
            q = q.Where(x => linked.Contains(x.Id));
        }

        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<InventoryDto>(rows.Select(MapInventory).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<InventoryDto> CreateInventoryAsync(CreateInventoryInput input, CancellationToken ct)
    {
        if (input.ResourceTypeId == Guid.Empty) throw Validation("A resource type is required.");
        var orgId = input.OrgId == Guid.Empty ? ScopedOrgId() ?? Guid.Empty : input.OrgId;
        if (orgId == Guid.Empty) throw Validation("An organisation is required.");
        if (!await db.ResourceTypes.AsNoTracking().AnyAsync(x => x.Id == input.ResourceTypeId, ct)) throw NotFound("RESOURCE_TYPE_NOT_FOUND", "The resource type was not found.");
        if (input.Quantity < 0 || input.ReorderThreshold < 0) throw Validation("Quantity and reorder threshold cannot be negative.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: true);

        var row = new Inventory
        {
            Id = Guid.NewGuid(),
            ResourceTypeId = input.ResourceTypeId,
            OrgId = orgId,
            Quantity = input.Quantity,
            Available = input.Quantity,
            Reserved = 0,
            Allocated = 0,
            Dispatched = 0,
            Utilised = 0,
            ReorderThreshold = input.ReorderThreshold,
            ExpiryDate = input.ExpiryDate,
            Lat = input.Lat,
            Lng = input.Lng,
            AccessibilityCapable = input.AccessibilityCapable
        };
        db.Inventories.Add(row);
        await db.SaveChangesAsync(ct);
        await AuditAsync("Inventory.Created", "Inventory", row.Id, new { row.ResourceTypeId, row.OrgId, row.Quantity }, ct);
        return MapInventory(row);
    }

    public async Task<InventoryDto> UpdateInventoryAsync(Guid id, UpdateInventoryInput input, CancellationToken ct)
    {
        var row = await db.Inventories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("INVENTORY_NOT_FOUND", "The inventory row was not found.");
        ScopedWrite(row.OrgId);
        if (input.Quantity < 0 || input.ReorderThreshold < 0) throw Validation("Quantity and reorder threshold cannot be negative.");
        var committed = row.Reserved + row.Allocated + row.Dispatched + row.Utilised;
        if (input.Quantity < committed) throw Conflict("INVENTORY_BELOW_COMMITTED", "Quantity cannot drop below reserved, allocated, dispatched and utilised stock.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: true);

        var delta = input.Quantity - row.Quantity;
        row.Quantity = input.Quantity;
        row.Available = Math.Max(0, row.Available + delta);
        row.ReorderThreshold = input.ReorderThreshold;
        row.ExpiryDate = input.ExpiryDate;
        row.Lat = input.Lat;
        row.Lng = input.Lng;
        row.AccessibilityCapable = input.AccessibilityCapable;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync("Inventory.Corrected", "Inventory", row.Id, new { input.Quantity, input.ReorderThreshold, delta }, ct);
        return MapInventory(row);
    }

    public async Task<AllocationDto> AllocateAsync(Guid decisionId, string idempotencyKey, CancellationToken ct)
    {
        var key = (idempotencyKey ?? string.Empty).Trim();
        if (key.Length == 0) await FailAsync("Allocation.Rejected", "Allocation", decisionId, new DomainException("An Idempotency-Key header is required.", "IDEMPOTENCY_KEY_REQUIRED"), ct);
        if (key.Length > 256) await FailAsync("Allocation.Rejected", "Allocation", decisionId, Validation("The Idempotency-Key cannot exceed 256 characters."), ct);

        var existing = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
        if (existing is not null) return MapAllocation(existing);

        // Prevent repeated execution of the same approved decision using a
        // different idempotency key: if any allocation already exists for
        // this decision, return it instead of allocating again.
        var existingByDecision = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.DecisionId == decisionId, ct);
        if (existingByDecision is not null) return MapAllocation(existingByDecision);

        var preflight = await DecisionRecordForAsync(decisionId, ct);
        var preflightPayload = ParsePayload(preflight.FinalPayloadJson);
        if (!TryGuid(preflightPayload, "requestId", out var requestId)) throw InvalidDecisionPayload();
        var decision = await decisionGate.RequireApprovedAsync(decisionId, RecommendationType.ResourceMatch, requestId, ct);
        var payload = ParsePayload(decision.FinalPayloadJson);
        if (!TryGuid(payload, "requestId", out requestId) ||
            !TryGuid(payload, "inventoryId", out var inventoryId) ||
            !TryDecimal(payload, "quantity", out var quantity) || quantity <= 0)
        {
            throw InvalidDecisionPayload();
        }

        IDbContextTransaction? tx = null;
        Allocation allocation;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(ct);

            if (tx is not null)
            {
                var changed = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE res.inventories SET available = available - {quantity}, allocated = allocated + {quantity}, updated_at = {DateTime.UtcNow} WHERE id = {inventoryId} AND available >= {quantity}", ct);
                if (changed != 1) throw Conflict("INSUFFICIENT_STOCK", "The selected inventory no longer has sufficient available stock.");
            }
            else
            {
                var inventory = await db.Inventories.FirstOrDefaultAsync(x => x.Id == inventoryId, ct);
                if (inventory is null || inventory.Available < quantity) throw Conflict("INSUFFICIENT_STOCK", "The selected inventory no longer has sufficient available stock.");
                inventory.Available -= quantity;
                inventory.Allocated += quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
            }

            allocation = new Allocation
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                InventoryId = inventoryId,
                Quantity = quantity,
                DecisionId = decision.Id,
                Status = "Allocated",
                Explanation = $"Allocated {quantity} units from approved ResourceMatch decision {decision.Id}.",
                IdempotencyKey = key
            };
            db.Allocations.Add(allocation);
            await EnqueueNotificationAsync(decision.Id, new { allocationId = allocation.Id, requestId, inventoryId, quantity, action = "allocated" }, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Roll back the failed insert before reading the winning row. SQL Server
            // leaves the transaction unusable after a constraint violation, so the
            // duplicate lookup must happen after the rollback.
            if (tx is not null) await tx.RollbackAsync(ct);
            var duplicate = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);
            if (duplicate is not null) return MapAllocation(duplicate);
            throw;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        await AuditAsync("Allocation.Created", "Allocation", allocation.Id, new { allocation.RequestId, allocation.InventoryId, allocation.Quantity }, ct);
        await RunPhase3Async(requestId, RequestStatus.Allocated, "Allocation", allocation.Id, "Stock committed; request status transition failed. Retry with POST /api/v1/operations/status-transitions/{id}/retry.", ct);
        return MapAllocation(allocation);
    }

    public async Task<PagedResult<AllocationDto>> GetAllocationsAsync(Guid? requestId, string? status, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Allocations.AsNoTracking().AsQueryable();
        if (requestId.HasValue) q = q.Where(x => x.RequestId == requestId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim());
        var scopedOrg = ScopedOrgId();
        if (scopedOrg.HasValue && !IsPrivileged())
        {
            q = q.Join(db.Inventories.AsNoTracking(), a => a.InventoryId, i => i.Id, (a, i) => new { a, i.OrgId })
                .Where(x => x.OrgId == scopedOrg.Value).Select(x => x.a);
        }
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<AllocationDto>(rows.Select(MapAllocation).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<DispatchDto> DispatchAsync(Guid decisionId, Guid? allocationId, CancellationToken ct)
    {
        var allocation = allocationId.HasValue
            ? await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == allocationId.Value, ct)
            : await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.DecisionId == decisionId, ct);
        if (allocation is null) throw NotFound("ALLOCATION_NOT_FOUND", "No allocation is linked to this decision.");
        await ScopedRead(allocation.InventoryId);

        var repeat = await db.Dispatches.AsNoTracking().FirstOrDefaultAsync(x => x.DecisionId == decisionId || x.AllocationId == allocation.Id, ct);
        if (repeat is not null) return MapDispatch(repeat);

        await decisionGate.RequireApprovedAsync(decisionId, RecommendationType.ResourceMatch, allocation.RequestId, ct);
        if (allocation.Status is not ("Allocated" or "Dispatched")) throw Conflict("INVALID_TRANSITION", "Only an Allocated or already-dispatched allocation can be dispatched.");

        IDbContextTransaction? tx = null;
        Dispatch dispatch;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(ct);
            if (allocation.Status == "Allocated")
            {
                if (tx is not null)
                {
                    var moved = await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE res.inventories SET allocated = allocated - {allocation.Quantity}, dispatched = dispatched + {allocation.Quantity}, updated_at = {DateTime.UtcNow} WHERE id = {allocation.InventoryId} AND allocated >= {allocation.Quantity}", ct);
                    if (moved != 1) throw Conflict("INVENTORY_BUCKET_MISMATCH", "The allocated bucket does not cover this dispatch.");
                }
                else
                {
                    var inventory = await db.Inventories.FirstOrDefaultAsync(x => x.Id == allocation.InventoryId, ct);
                    if (inventory is null || inventory.Allocated < allocation.Quantity) throw Conflict("INVENTORY_BUCKET_MISMATCH", "The allocated bucket does not cover this dispatch.");
                    inventory.Allocated -= allocation.Quantity;
                    inventory.Dispatched += allocation.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;
                }
                var tracked = await db.Allocations.FirstAsync(x => x.Id == allocation.Id, ct);
                tracked.Status = "Dispatched";
            }

            dispatch = new Dispatch
            {
                Id = Guid.NewGuid(),
                AllocationId = allocation.Id,
                DecisionId = decisionId,
                Quantity = allocation.Quantity,
                Status = "Dispatched"
            };
            db.Dispatches.Add(dispatch);
            await EnqueueNotificationAsync(decisionId, new { dispatchId = dispatch.Id, allocationId = allocation.Id, requestId = allocation.RequestId, action = "dispatched" }, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        await AuditAsync("Allocation.Dispatched", "Allocation", allocation.Id, new { dispatch.Id, decisionId }, ct);
        await RunPhase3Async(allocation.RequestId, RequestStatus.Dispatched, "Dispatch", dispatch.Id, "Dispatch recorded; request status transition failed. Retry with POST /api/v1/operations/status-transitions/{id}/retry.", ct);
        return MapDispatch(dispatch);
    }

    public async Task<DispatchDto> ConfirmDeliveryAsync(Guid decisionId, Guid? dispatchId, CancellationToken ct)
    {
        var dispatch = dispatchId.HasValue
            ? await db.Dispatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dispatchId.Value, ct)
            : await db.Dispatches.AsNoTracking().FirstOrDefaultAsync(x => x.DecisionId == decisionId, ct);
        if (dispatch is null) throw NotFound("DISPATCH_NOT_FOUND", "No dispatch is linked to this decision.");
        var allocation = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dispatch.AllocationId, ct) ?? throw NotFound("ALLOCATION_NOT_FOUND", "The linked allocation was not found.");
        await ScopedRead(allocation.InventoryId);
        if (dispatch.Status == "Fulfilled") return MapDispatch(dispatch);
        if (dispatch.Status != "Dispatched") throw Conflict("INVALID_TRANSITION", "Only a dispatched delivery can be confirmed.");

        await decisionGate.RequireApprovedAsync(decisionId, RecommendationType.ResourceMatch, allocation.RequestId, ct);

        IDbContextTransaction? tx = null;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(ct);
            if (tx is not null)
            {
                var moved = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE res.inventories SET dispatched = dispatched - {dispatch.Quantity}, utilised = utilised + {dispatch.Quantity}, updated_at = {DateTime.UtcNow} WHERE id = {allocation.InventoryId} AND dispatched >= {dispatch.Quantity}", ct);
                if (moved != 1) throw Conflict("INVENTORY_BUCKET_MISMATCH", "The dispatched bucket does not cover this confirmation.");
            }
            else
            {
                var inventory = await db.Inventories.FirstOrDefaultAsync(x => x.Id == allocation.InventoryId, ct);
                if (inventory is null || inventory.Dispatched < dispatch.Quantity) throw Conflict("INVENTORY_BUCKET_MISMATCH", "The dispatched bucket does not cover this confirmation.");
                inventory.Dispatched -= dispatch.Quantity;
                inventory.Utilised += dispatch.Quantity;
                inventory.UpdatedAt = DateTime.UtcNow;
            }

            var trackedDispatch = await db.Dispatches.FirstAsync(x => x.Id == dispatch.Id, ct);
            var trackedAllocation = await db.Allocations.FirstAsync(x => x.Id == allocation.Id, ct);
            trackedDispatch.Status = "Fulfilled";
            trackedDispatch.ConfirmedAt = DateTime.UtcNow;
            trackedDispatch.ConfirmedBy = user.UserId;
            trackedAllocation.Status = "Fulfilled";
            await EnqueueNotificationAsync(decisionId, new { dispatchId = dispatch.Id, requestId = allocation.RequestId, action = "delivered" }, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        await AuditAsync("Allocation.Fulfilled", "Allocation", allocation.Id, new { dispatch.Id, decisionId }, ct);
        await RunPhase3Async(allocation.RequestId, RequestStatus.Fulfilled, "Delivery", dispatch.Id, "Delivery confirmed; request status transition failed. Retry with POST /api/v1/operations/status-transitions/{id}/retry.", ct);
        var saved = await db.Dispatches.AsNoTracking().FirstAsync(x => x.Id == dispatch.Id, ct);
        return MapDispatch(saved);
    }

    public async Task<PagedResult<DispatchDto>> GetDispatchesAsync(Guid? allocationId, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Dispatches.AsNoTracking().AsQueryable();
        if (allocationId.HasValue) q = q.Where(x => x.AllocationId == allocationId.Value);
        var scopedOrg = ScopedOrgId();
        if (scopedOrg.HasValue && !IsPrivileged())
        {
            q = q.Join(db.Allocations.AsNoTracking(), d => d.AllocationId, a => a.Id, (d, a) => new { d, a.InventoryId })
                .Join(db.Inventories.AsNoTracking(), x => x.InventoryId, i => i.Id, (x, i) => new { x.d, i.OrgId })
                .Where(x => x.OrgId == scopedOrg.Value).Select(x => x.d);
        }
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<DispatchDto>(rows.Select(MapDispatch).ToArray(), total, page < 1 ? 1 : page, size);
    }

    // -----------------------------------------------------------------------
    // M6 shelters.
    // -----------------------------------------------------------------------
    public async Task<PagedResult<ShelterDto>> GetSheltersAsync(bool? approved, Guid? orgId, int page, int pageSize, CancellationToken ct)
    {
        var q = db.Shelters.AsNoTracking().AsQueryable();
        if (approved.HasValue) q = q.Where(x => x.Approved == approved.Value);
        if (orgId.HasValue) q = q.Where(x => x.OrgId == orgId.Value);
        var scopedOrg = ScopedOrgId();
        if (scopedOrg.HasValue && !IsPrivileged()) q = q.Where(x => x.OrgId == scopedOrg.Value);
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderBy(x => x.Name).ThenBy(x => x.Id).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<ShelterDto>(rows.Select(MapShelter).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<IReadOnlyList<ShelterDto>> GetPublicSheltersAsync(CancellationToken ct)
    {
        var rows = await db.Shelters.AsNoTracking().Where(x => x.Approved).OrderBy(x => x.Name).ToListAsync(ct);
        // Public projection deliberately omits OrgId, contact data and any PII.
        return rows.Select(x => new ShelterDto(x.Id, x.Name, x.Capacity, x.Occupancy, x.Capacity - x.Occupancy,
            x.Facilities, x.AccessibilityFeatures, x.Services, x.Lat, x.Lng, Guid.Empty, true)).ToArray();
    }

    public async Task<ShelterDto> CreateShelterAsync(CreateShelterInput input, CancellationToken ct)
    {
        var name = (input.Name ?? string.Empty).Trim();
        if (name.Length is 0 or > 160) throw Validation("A shelter name (max 160 characters) is required.");
        if (input.Capacity < 1) throw Validation("Capacity must be at least one.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: false);
        var orgId = input.OrgId == Guid.Empty ? ScopedOrgId() ?? Guid.Empty : input.OrgId;
        if (orgId == Guid.Empty) throw Validation("An organisation is required.");

        var row = new Shelter
        {
            Id = Guid.NewGuid(),
            Name = name,
            Capacity = input.Capacity,
            Occupancy = 0,
            Facilities = NormalizeJsonArray(input.Facilities),
            AccessibilityFeatures = NormalizeJsonArray(input.AccessibilityFeatures),
            Services = NormalizeJsonArray(input.Services),
            Lat = input.Lat,
            Lng = input.Lng,
            OrgId = orgId,
            Approved = false
        };
        db.Shelters.Add(row);
        await db.SaveChangesAsync(ct);
        await AuditAsync("Shelter.Created", "Shelter", row.Id, new { row.Name, row.Capacity, row.OrgId }, ct);
        return MapShelter(row);
    }

    public async Task<ShelterDto> UpdateShelterAsync(Guid id, UpdateShelterInput input, CancellationToken ct)
    {
        var row = await db.Shelters.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("SHELTER_NOT_FOUND", "The shelter was not found.");
        ScopedWrite(row.OrgId);
        if (input.Capacity < row.Occupancy) throw Conflict("SHELTER_CAPACITY_BELOW_OCCUPANCY", "Capacity cannot be lower than current occupancy.");
        if (input.Capacity < 1) throw Validation("Capacity must be at least one.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: false);
        row.Name = input.Name.Trim();
        row.Capacity = input.Capacity;
        row.Facilities = NormalizeJsonArray(input.Facilities);
        row.AccessibilityFeatures = NormalizeJsonArray(input.AccessibilityFeatures);
        row.Services = NormalizeJsonArray(input.Services);
        row.Lat = input.Lat;
        row.Lng = input.Lng;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync("Shelter.Updated", "Shelter", row.Id, new { input.Capacity, row.Occupancy }, ct);
        return MapShelter(row);
    }

    public async Task<ShelterDto> ApproveShelterAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Shelters.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("SHELTER_NOT_FOUND", "The shelter was not found.");
        row.Approved = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync("Shelter.Approved", "Shelter", row.Id, new { row.Name }, ct);
        return MapShelter(row);
    }

    public async Task<ShelterAllocationDto> AllocateShelterAsync(Guid decisionId, CancellationToken ct)
    {
        var preflight = await DecisionRecordForAsync(decisionId, ct);
        var preflightPayload = ParsePayload(preflight.FinalPayloadJson);
        if (!TryGuid(preflightPayload, "requestId", out var requestId)) throw InvalidDecisionPayload();
        var decision = await decisionGate.RequireApprovedAsync(decisionId, RecommendationType.ShelterMatch, requestId, ct);
        var payload = ParsePayload(decision.FinalPayloadJson);
        if (!TryGuid(payload, "requestId", out requestId) ||
            !TryGuid(payload, "shelterId", out var shelterId) ||
            !TryInt(payload, "householdSize", out var count) || count < 1)
        {
            throw InvalidDecisionPayload();
        }

        IDbContextTransaction? tx = null;
        ShelterAllocation item;
        try
        {
            if (db.Database.IsRelational()) tx = await db.Database.BeginTransactionAsync(ct);
            if (tx is not null)
            {
                var rows = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE shelter.shelters SET occupancy = occupancy + {count}, updated_at = {DateTime.UtcNow} WHERE id = {shelterId} AND approved = 1 AND occupancy + {count} <= capacity", ct);
                if (rows != 1) throw Conflict("SHELTER_FULL", "The shelter cannot accommodate this household.");
            }
            else
            {
                var shelter = await db.Shelters.FirstOrDefaultAsync(x => x.Id == shelterId, ct);
                if (shelter is null || !shelter.Approved || shelter.Occupancy + count > shelter.Capacity) throw Conflict("SHELTER_FULL", "The shelter cannot accommodate this household.");
                shelter.Occupancy += count;
                shelter.UpdatedAt = DateTime.UtcNow;
            }

            item = new ShelterAllocation { Id = Guid.NewGuid(), RequestId = requestId, ShelterId = shelterId, HouseholdSize = count, DecisionId = decision.Id };
            db.ShelterAllocations.Add(item);
            await EnqueueNotificationAsync(decision.Id, new { shelterAllocationId = item.Id, requestId, shelterId, householdSize = count, action = "shelter-allocated" }, ct);
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique decision index lets a duplicate submission replay the
            // original allocation. Roll back first so the failed transaction is
            // usable for the lookup and the losing occupancy increment is undone.
            if (tx is not null) await tx.RollbackAsync(ct);
            var duplicate = await db.ShelterAllocations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.DecisionId == decision.Id, ct);
            if (duplicate is not null) return new ShelterAllocationDto(
                duplicate.Id, duplicate.RequestId, duplicate.ShelterId,
                duplicate.HouseholdSize, duplicate.DecisionId, duplicate.CreatedAt);
            throw;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        await AuditAsync("Shelter.Allocated", "ShelterAllocation", item.Id, new { item.RequestId, item.ShelterId, item.HouseholdSize }, ct);
        await RunPhase3Async(requestId, RequestStatus.Allocated, "Shelter allocation", item.Id, "Shelter occupancy committed; request status transition failed. Retry with POST /api/v1/operations/status-transitions/{id}/retry.", ct);
        return new ShelterAllocationDto(item.Id, item.RequestId, item.ShelterId, item.HouseholdSize, item.DecisionId, item.CreatedAt);
    }

    // -----------------------------------------------------------------------
    // M7 volunteers.
    // -----------------------------------------------------------------------
    public async Task<IReadOnlyList<VolunteerDto>> GetVolunteersAsync(CancellationToken ct)
    {
        var rows = await db.Volunteers.AsNoTracking().OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return rows.Select(MapVolunteer).ToArray();
    }

    public async Task<VolunteerDto> SaveVolunteerProfileAsync(SaveVolunteerProfileInput input, CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();
        if (!IsValidJsonArray(input.SkillsJson) || !IsValidJsonArray(input.LanguagesJson)) throw Validation("Skills and languages must be JSON arrays of strings.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: true);
        var item = await db.Volunteers.FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct);
        if (item is null)
        {
            item = new Volunteer { Id = Guid.NewGuid(), UserId = user.UserId.Value, VerificationStatus = "Pending" };
            db.Volunteers.Add(item);
        }
        item.SkillsJson = input.SkillsJson.Trim();
        item.LanguagesJson = input.LanguagesJson.Trim();
        item.Lat = input.Lat;
        item.Lng = input.Lng;
        item.Active = input.Active;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await AuditAsync("Volunteer.ProfileUpdated", "Volunteer", item.Id, new { item.Active, skills = ParseArray(item.SkillsJson) }, ct);
        return MapVolunteer(item);
    }

    public async Task<VolunteerDto> VerifyVolunteerAsync(Guid id, bool approved, string? reason, CancellationToken ct)
    {
        var item = await db.Volunteers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("VOLUNTEER_NOT_FOUND", "The volunteer was not found.");
        if (item.VerificationStatus == "Rejected") throw Conflict("VOLUNTEER_REJECTED", "A rejected profile cannot be re-verified through this endpoint.");
        if (string.IsNullOrWhiteSpace(reason)) throw Validation("A verification reason is required.");
        var status = approved ? "Verified" : "Rejected";
        item.VerificationStatus = status;
        item.VerificationReason = reason.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await EnqueueNotificationAsync(id, new { volunteerId = id, userId = item.UserId, status, reason = item.VerificationReason, action = "volunteer-verification" }, ct, $"volunteer-verification-{status}");
        await db.SaveChangesAsync(ct);
        await AuditAsync("Volunteer." + item.VerificationStatus, "Volunteer", id, new { reason = item.VerificationReason }, ct);
        return MapVolunteer(item);
    }

    public async Task<VolunteerAvailabilityDto> AddAvailabilityAsync(AddAvailabilityInput input, CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct) ?? throw NotFound("VOLUNTEER_NOT_FOUND", "Create a volunteer profile first.");
        if (input.EndsAt <= input.StartsAt) throw Validation("The availability window must have an end after its start.");
        if (input.StartsAt < DateTime.UtcNow.AddMinutes(-1)) throw Validation("Availability cannot start in the past.");
        var row = new VolunteerAvailability { Id = Guid.NewGuid(), VolunteerId = volunteer.Id, StartsAt = input.StartsAt, EndsAt = input.EndsAt };
        db.VolunteerAvailabilities.Add(row);
        await db.SaveChangesAsync(ct);
        await AuditAsync("Volunteer.AvailabilityAdded", "VolunteerAvailability", row.Id, new { row.StartsAt, row.EndsAt }, ct);
        return new VolunteerAvailabilityDto(row.Id, row.VolunteerId, row.StartsAt, row.EndsAt);
    }

    public async Task<PagedResult<VolunteerTaskDto>> GetMyTasksAsync(int page, int pageSize, CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();
        var volunteer = await db.Volunteers.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct);
        NormalizePage(page, pageSize, out var emptySize);
        if (volunteer is null) return new PagedResult<VolunteerTaskDto>([], 0, page < 1 ? 1 : page, emptySize);
        var q = db.VolunteerTasks.AsNoTracking().Where(x => x.VolunteerId == volunteer.Id);
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<VolunteerTaskDto>(rows.Select(MapTask).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<VolunteerPortalDto> GetVolunteerPortalAsync(CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();

        var opportunities = await db.VolunteerTasks.AsNoTracking()
            .Where(x => x.VolunteerId == null && x.Status == "Open")
            .OrderBy(x => x.ScheduledAt).ThenBy(x => x.CreatedAt)
            .Take(20).ToListAsync(ct);

        var profile = await db.Volunteers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct);

        if (profile is null)
        {
            return new VolunteerPortalDto(
                null,
                Array.Empty<VolunteerAvailabilityDto>(),
                0,
                opportunities.Select(MapTask).ToArray());
        }

        var availability = await db.VolunteerAvailabilities.AsNoTracking()
            .Where(x => x.VolunteerId == profile.Id)
            .OrderBy(x => x.StartsAt).Take(20).ToListAsync(ct);

        var completed = await db.VolunteerActivities.AsNoTracking()
            .CountAsync(x => x.VolunteerId == profile.Id, ct);

        return new VolunteerPortalDto(
            MapVolunteer(profile),
            availability.Select(x =>
                new VolunteerAvailabilityDto(x.Id, x.VolunteerId, x.StartsAt, x.EndsAt)).ToArray(),
            completed,
            opportunities.Select(MapTask).ToArray());
    }

    public async Task<PagedResult<VolunteerTaskDto>> GetVolunteerTasksAsync(string? status, int page, int pageSize, CancellationToken ct)
    {
        var q = db.VolunteerTasks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim());
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderBy(x => x.ScheduledAt).ThenBy(x => x.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<VolunteerTaskDto>(rows.Select(MapTask).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<VolunteerTaskDto> CreateVolunteerTaskAsync(CreateVolunteerTaskInput input, CancellationToken ct)
    {
        var title = (input.Title ?? string.Empty).Trim();
        if (title.Length is 0 or > 160) throw Validation("A task title (max 160 characters) is required.");
        if (!IsValidJsonArray(input.RequiredSkillsJson)) throw Validation("Required skills must be a JSON array of strings.");
        ValidateCoordinates(input.Lat, input.Lng, allowNull: true);
        var row = new VolunteerTask
        {
            Id = Guid.NewGuid(),
            RequestId = input.RequestId,
            Title = title,
            RequiredSkillsJson = input.RequiredSkillsJson.Trim(),
            Lat = input.Lat,
            Lng = input.Lng,
            ScheduledAt = input.ScheduledAt,
            Status = "Open"
        };
        db.VolunteerTasks.Add(row);
        await db.SaveChangesAsync(ct);
        await AuditAsync("VolunteerTask.Created", "VolunteerTask", row.Id, new { row.Title, row.RequestId }, ct);
        return MapTask(row);
    }

    public async Task<VolunteerTaskDto> AssignVolunteerTaskAsync(Guid id, Guid volunteerId, CancellationToken ct)
    {
        var task = await db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("TASK_NOT_FOUND", "The task was not found.");
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(x => x.Id == volunteerId, ct) ?? throw NotFound("VOLUNTEER_NOT_FOUND", "The volunteer was not found.");
        if (task.Status != "Open") throw Conflict("INVALID_TRANSITION", "Only an open task can be assigned.");
        if (!volunteer.Active || volunteer.VerificationStatus != "Verified") throw new ForbiddenException("Only verified, active volunteers can be assigned.", "VOLUNTEER_NOT_ASSIGNABLE");
        var now = DateTime.UtcNow;
        if (!await db.VolunteerAvailabilities.AsNoTracking().AnyAsync(x => x.VolunteerId == volunteerId && x.StartsAt <= now && x.EndsAt >= now, ct)) throw Conflict("VOLUNTEER_NOT_AVAILABLE", "The volunteer has no availability covering this assignment.");
        task.VolunteerId = volunteerId;
        task.Status = "Assigned";
        task.UpdatedAt = now;
        await EnqueueNotificationAsync(id, new { taskId = id, volunteerId, userId = volunteer.UserId, action = "task-assigned" }, ct, "task-assigned");
        await db.SaveChangesAsync(ct);
        await AuditAsync("VolunteerTask.Assigned", "VolunteerTask", id, new { volunteerId }, ct);
        return MapTask(task);
    }

    public async Task<VolunteerTaskDto> RespondToTaskAsync(Guid id, bool accept, string? reason, CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct) ?? throw NotFound("VOLUNTEER_NOT_FOUND", "Create a volunteer profile first.");
        var task = await db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("TASK_NOT_FOUND", "The task was not found.");
        if (task.VolunteerId != volunteer.Id || task.Status != "Assigned") throw new ForbiddenException("This task is not assigned to you.", "TASK_NOT_ASSIGNED");
        task.Status = accept ? "Accepted" : "Open";
        if (!accept) task.VolunteerId = null;
        task.UpdatedAt = DateTime.UtcNow;
        await EnqueueNotificationAsync(id, new { taskId = id, volunteerId = volunteer.Id, userId = volunteer.UserId, accepted = accept, reason, action = "task-response" }, ct, "task-response");
        await db.SaveChangesAsync(ct);
        await AuditAsync("VolunteerTask." + task.Status, "VolunteerTask", id, new { accepted = accept, reason }, ct);
        return MapTask(task);
    }

    public async Task<VolunteerTaskDto> CompleteTaskAsync(Guid id, CancellationToken ct)
    {
        if (!user.UserId.HasValue) throw new UnauthorizedAccessException();
        var volunteer = await db.Volunteers.FirstOrDefaultAsync(x => x.UserId == user.UserId.Value, ct) ?? throw NotFound("VOLUNTEER_NOT_FOUND", "Create a volunteer profile first.");
        var task = await db.VolunteerTasks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("TASK_NOT_FOUND", "The task was not found.");
        if (task.VolunteerId != volunteer.Id || task.Status != "Accepted") throw new ForbiddenException("Only the assigned volunteer can complete an accepted task.", "TASK_NOT_ACCEPTED");
        var now = DateTime.UtcNow;
        task.Status = "Completed";
        task.CompletedAt = now;
        task.UpdatedAt = now;
        db.VolunteerActivities.Add(new VolunteerActivity { Id = Guid.NewGuid(), VolunteerId = volunteer.Id, TaskId = id, CompletedBy = user.UserId.Value, CompletedAt = now });
        await EnqueueNotificationAsync(id, new { taskId = id, volunteerId = volunteer.Id, userId = volunteer.UserId, completedAt = now, action = "task-completed" }, ct, "task-completed");
        await db.SaveChangesAsync(ct);
        await AuditAsync("VolunteerTask.Completed", "VolunteerTask", id, new { volunteer.Id, completedAt = now }, ct);
        return MapTask(task);
    }

    // -----------------------------------------------------------------------
    // M5 shortage engine / assistance requests.
    // -----------------------------------------------------------------------
    public async Task<AssistanceRequestDto> CreateAssistanceRequestAsync(CreateAssistanceRequestInput input, CancellationToken ct)
    {
        var scopedOrg = ScopedOrgId();
        var fromOrg = input.FromOrgId == Guid.Empty ? scopedOrg ?? Guid.Empty : input.FromOrgId;
        if (fromOrg == Guid.Empty || input.ResourceTypeId == Guid.Empty || input.Quantity <= 0 || string.IsNullOrWhiteSpace(input.Reason))
            throw Validation("From organisation, resource type, positive quantity and a reason are required.");
        if (!IsPrivileged() && (!scopedOrg.HasValue || fromOrg != scopedOrg.Value))
            throw new ForbiddenException("An organisation can only create assistance requests on its own behalf.", "ASSISTANCE_SOURCE_ORG_REQUIRED");
        if (!await db.ResourceTypes.AsNoTracking().AnyAsync(x => x.Id == input.ResourceTypeId, ct)) throw NotFound("RESOURCE_TYPE_NOT_FOUND", "The resource type was not found.");
        if (input.ShelterId.HasValue)
        {
            var shelter = await db.Shelters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == input.ShelterId.Value, ct) ?? throw NotFound("SHELTER_NOT_FOUND", "The shelter was not found.");
            var org = ScopedOrgId();
            if (!IsPrivileged() && org.HasValue && shelter.OrgId != org.Value) throw new ForbiddenException("The shelter belongs to another organisation.", "SHELTER_ORG_MISMATCH");
        }
        var row = new AssistanceRequest
        {
            Id = Guid.NewGuid(),
            FromOrgId = fromOrg,
            ToOrgId = input.ToOrgId,
            ResourceTypeId = input.ResourceTypeId,
            Quantity = input.Quantity,
            Reason = input.Reason.Trim(),
            ShelterId = input.ShelterId,
            Status = "Pending"
        };
        db.AssistanceRequests.Add(row);
        await EnqueueNotificationAsync(row.Id, new { assistanceRequestId = row.Id, toOrgId = row.ToOrgId, quantity = row.Quantity, shelterId = row.ShelterId, action = "assistance-requested" }, ct);
        await db.SaveChangesAsync(ct);
        await AuditAsync("AssistanceRequest.Created", "AssistanceRequest", row.Id, new { row.FromOrgId, row.ToOrgId, row.ResourceTypeId, row.Quantity, row.ShelterId }, ct);
        return MapAssistance(row);
    }

    public async Task<AssistanceRequestDto> RespondAssistanceRequestAsync(Guid id, bool accept, string? reason, CancellationToken ct)
    {
        var row = await db.AssistanceRequests.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw NotFound("ASSISTANCE_REQUEST_NOT_FOUND", "The assistance request was not found.");
        if (row.Status != "Pending") throw Conflict("INVALID_TRANSITION", "Only a pending assistance request can be accepted or rejected.");
        var org = ScopedOrgId();
        var target = row.ToOrgId ?? Guid.Empty;
        if (!IsPrivileged() && (!org.HasValue || target == Guid.Empty || org.Value != target))
            throw new ForbiddenException("Only the target organisation can respond to this request. Requests without a target organisation cannot be accepted or rejected.", "ASSISTANCE_TARGET_ORG_REQUIRED");
        if (string.IsNullOrWhiteSpace(reason)) throw Validation("A response reason is required.");
        row.Status = accept ? "Accepted" : "Rejected";
        row.ResponseReason = reason.Trim();
        row.UpdatedAt = DateTime.UtcNow;
        await EnqueueNotificationAsync(row.Id, new { assistanceRequestId = row.Id, fromOrgId = row.FromOrgId, status = row.Status, reason = row.ResponseReason, action = "assistance-response" }, ct);
        await db.SaveChangesAsync(ct);
        await AuditAsync("AssistanceRequest." + row.Status, "AssistanceRequest", id, new { reason = row.ResponseReason }, ct);
        return MapAssistance(row);
    }

    public async Task<PagedResult<AssistanceRequestDto>> GetAssistanceRequestsAsync(string? direction, string? status, int page, int pageSize, CancellationToken ct)
    {
        var q = db.AssistanceRequests.AsNoTracking().AsQueryable();
        var org = ScopedOrgId();
        if (string.Equals(direction, "in", StringComparison.OrdinalIgnoreCase)) q = q.Where(x => org.HasValue && x.ToOrgId == org.Value);
        else if (string.Equals(direction, "out", StringComparison.OrdinalIgnoreCase)) q = q.Where(x => org.HasValue && x.FromOrgId == org.Value);
        else if (org.HasValue && !IsPrivileged()) q = q.Where(x => x.FromOrgId == org.Value || x.ToOrgId == org.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status.Trim());
        var total = await q.CountAsync(ct);
        var skip = NormalizePage(page, pageSize, out var size);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(size).ToListAsync(ct);
        return new PagedResult<AssistanceRequestDto>(rows.Select(MapAssistance).ToArray(), total, page < 1 ? 1 : page, size);
    }

    public async Task<ShortageProposalDto> GetShortageProposalAsync(Guid resourceTypeId, CancellationToken ct)
    {
        if (resourceTypeId == Guid.Empty) throw Validation("A resource type is required.");
        var type = await db.ResourceTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == resourceTypeId, ct) ?? throw NotFound("RESOURCE_TYPE_NOT_FOUND", "The resource type was not found.");
        var stock = await db.Inventories.AsNoTracking().Where(x => x.ResourceTypeId == resourceTypeId).SumAsync(x => (decimal?)x.Available, ct) ?? 0;
        var requestIds = await db.Requests.AsNoTracking()
            .Where(x => x.Status != RequestStatus.Closed && x.Status != RequestStatus.Fulfilled && x.Status != RequestStatus.Rejected)
            .Select(x => x.Id).ToListAsync(ct);

        var summaries = new List<RequestSummaryDto>();
        foreach (var id in requestIds)
        {
            var summary = await requestReader.GetAsync(id, ct);
            if (summary is not null && MatchesCategory(type.Name, summary.Category)) summaries.Add(summary);
        }

        var ranked = summaries
            .OrderByDescending(x => x.Urgency)
            .ThenByDescending(x => HasAccessibilityNeed(x))
            .ThenBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToArray();

        var remaining = stock;
        var items = new List<ShortageProposalItemDto>(ranked.Length);
        for (var i = 0; i < ranked.Length; i++)
        {
            var request = ranked[i];
            var requested = EstimateRequestedQuantity(request);
            var proposed = Math.Min(requested, Math.Max(0, remaining));
            remaining -= proposed;
            var signals = $"urgency {request.Urgency}, {(HasAccessibilityNeed(request) ? "accessibility need" : "no accessibility need")}, request age";
            var explanation = proposed >= requested
                ? $"Priority {i + 1}: enough of the available {stock} {type.Unit} to meet the request. Ranked by {signals}. No stock was reserved; a human decision is required."
                : $"Priority {i + 1}: only {proposed} of {requested} {type.Unit} can be offered from {stock} available. Ranked by {signals}; partial fulfilment is proposed. No stock was reserved; a human decision is required.";
            items.Add(new ShortageProposalItemDto(request.Id, request.Category.ToString(), request.Urgency.ToString(), requested, stock, i + 1, proposed, explanation));
        }

        return new ShortageProposalDto(resourceTypeId, stock, items, false, true);
    }

    // -----------------------------------------------------------------------
    // Retry-safe Phase 3 status transitions.
    // -----------------------------------------------------------------------
    public async Task<IReadOnlyList<PendingStatusTransitionDto>> GetPendingTransitionsAsync(CancellationToken ct)
    {
        var result = new List<PendingStatusTransitionDto>();
        var statuses = await db.Requests.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Status, ct);

        foreach (var a in await db.Allocations.AsNoTracking().ToListAsync(ct))
        {
            if (statuses.TryGetValue(a.RequestId, out var status) && NeedsTransition(status, RequestStatus.Allocated))
                result.Add(new PendingStatusTransitionDto(a.Id, "Allocation", a.Id, a.RequestId, RequestStatus.Allocated.ToString(), 1, "Request status transition did not complete.", null));
        }
        foreach (var d in await db.Dispatches.AsNoTracking().ToListAsync(ct))
        {
            var allocation = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == d.AllocationId, ct);
            if (allocation is null || !statuses.TryGetValue(allocation.RequestId, out var status)) continue;
            var target = d.Status == "Fulfilled" ? RequestStatus.Fulfilled : RequestStatus.Dispatched;
            if (NeedsTransition(status, target))
                result.Add(new PendingStatusTransitionDto(d.Id, "Dispatch", d.Id, allocation.RequestId, target.ToString(), 1, "Request status transition did not complete.", null));
        }
        return result.OrderBy(x => x.EntityId).ToArray();
    }

    public async Task<PendingStatusTransitionDto?> RetryStatusTransitionAsync(Guid id, CancellationToken ct)
    {
        var allocation = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (allocation is not null)
        {
            var current = await CurrentRequestStatusAsync(allocation.RequestId, ct);
            if (current != RequestStatus.Allocated)
            {
                EnsureRetryable(current, RequestStatus.Allocated);
                await requestStatus.TransitionAsync(allocation.RequestId, RequestStatus.Allocated, "Retry after allocation status-transition failure.", ct);
            }
            await AuditAsync("Allocation.StatusTransitionRetried", "Allocation", id, new { allocation.RequestId }, ct);
            return new PendingStatusTransitionDto(id, "Allocation", id, allocation.RequestId, RequestStatus.Allocated.ToString(), 1, null, DateTime.UtcNow);
        }
        var dispatch = await db.Dispatches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (dispatch is null) return null;
        var linked = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dispatch.AllocationId, ct) ?? throw NotFound("ALLOCATION_NOT_FOUND", "The linked allocation was not found.");
        var target = dispatch.Status == "Fulfilled" ? RequestStatus.Fulfilled : RequestStatus.Dispatched;
        var dispatchCurrent = await CurrentRequestStatusAsync(linked.RequestId, ct);
        if (dispatchCurrent != target)
        {
            EnsureRetryable(dispatchCurrent, target);
            await requestStatus.TransitionAsync(linked.RequestId, target, "Retry after dispatch status-transition failure.", ct);
        }
        await AuditAsync("Dispatch.StatusTransitionRetried", "Dispatch", id, new { linked.RequestId, target }, ct);
        return new PendingStatusTransitionDto(id, "Dispatch", id, linked.RequestId, target.ToString(), 1, null, DateTime.UtcNow);
    }

    // -----------------------------------------------------------------------
    // Matchers (read-only).
    // -----------------------------------------------------------------------
    public Task<IReadOnlyList<MatchCandidate>> RankResourcesAsync(Guid requestId, CancellationToken ct) => resourceMatcher.RankAsync(requestId, ct);
    public Task<IReadOnlyList<MatchCandidate>> RankSheltersAsync(Guid requestId, CancellationToken ct) => shelterRecommender.RankAsync(requestId, ct);
    public Task<IReadOnlyList<MatchCandidate>> RankVolunteersAsync(Guid requestId, CancellationToken ct) => volunteerMatcher.RankAsync(requestId, ct);

    public async Task<IReadOnlyList<MapAreaDto>> GetMapAreasAsync(CancellationToken ct)
    {
        // Dedicated Member 4 read so every role allowed to open the operations
        // map can see area markers without widening theDev2 case-reader policy.
        return await db.Set<Area>().AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new MapAreaDto(a.Id, a.CrisisId, a.Name, a.Lat, a.Lng, a.RadiusKm))
            .ToListAsync(ct);
    }

    public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct) => geo.GeocodeAsync(address, ct);

    public async Task<DistanceResult> GetDistanceAsync(double fromLat, double fromLng, double toLat, double toLng, CancellationToken ct)
    {
        if (!FiniteCoordinate(fromLat, fromLng) || !FiniteCoordinate(toLat, toLng)) throw Validation("Coordinates must be finite and within latitude/longitude bounds.");
        return await geo.GetDistanceAsync(new GeoPoint(fromLat, fromLng), new GeoPoint(toLat, toLng), ct);
    }

    // -----------------------------------------------------------------------
    // Helpers.
    // -----------------------------------------------------------------------
    private async Task RunPhase3Async(Guid requestId, RequestStatus target, string operation, Guid entityId, string failureHint, CancellationToken ct)
    {
        try
        {
            await requestStatus.TransitionAsync(requestId, target, $"{operation} committed by Member 4 operations service.", ct);
        }
        catch (Exception ex)
        {
            await audit.LogAsync(user.UserId, "Allocation.StatusTransitionFailed", "Request", requestId.ToString(), new { operation, entityId, target = target.ToString(), error = ex.Message, failureHint }, ct);
        }
    }

    private async Task EnqueueNotificationAsync(Guid entityId, object payload, CancellationToken ct, string? idempotencySuffix = null)
    {
        // The Notification.Send contract requires a recipient and template key.
        // Normalise the Member 4 activity payloads here so the registered handler
        // can deliver them instead of retrying and dead-lettering malformed events.
        var json = JsonSerializer.Serialize(payload, payload.GetType());
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                  ?? new Dictionary<string, JsonElement>();
        var values = new Dictionary<string, JsonElement>(raw, StringComparer.OrdinalIgnoreCase);

        var recipient = ReadGuid(values, "RecipientUserId", "UserId", "userId");
        var requestId = ReadGuid(values, "RequestId", "requestId");
        var assistanceRequestId = ReadGuid(values, "AssistanceRequestId", "assistanceRequestId");

        if (recipient is null && (requestId is not null || assistanceRequestId is not null))
        {
            var ownerId = requestId ?? assistanceRequestId!.Value;
            recipient = await db.Requests.AsNoTracking()
                .Where(row => row.Id == ownerId)
                .Select(row => (Guid?)row.RequesterUserId)
                .FirstOrDefaultAsync(ct);
        }

        if (recipient is null)
        {
            await AuditAsync("Notification.Skipped", "Outbox", entityId,
                new { Reason = "No recipient could be resolved for the Notification.Send event." }, ct);
            return;
        }

        var action = ReadString(values, "Action", "action");
        var templateKey = ReadString(values, "TemplateKey", "templateKey")
                          ?? (string.IsNullOrWhiteSpace(action) ? "Operations.Notification" : $"Operations.{action}");
        var data = values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ValueKind == JsonValueKind.String
                ? pair.Value.GetString() ?? string.Empty
                : pair.Value.GetRawText(),
            StringComparer.OrdinalIgnoreCase);

        var notification = new NotificationPayloadDto
        {
            RecipientUserId = recipient.Value,
            TemplateKey = templateKey,
            RequestId = requestId,
            Data = data
        };

        // Keep the shared key format untouched. A local suffix only distinguishes
        // lifecycle events that intentionally share one entity id.
        var idempotencyKey = OutboxEventTypes.BuildIdempotencyKey(OutboxEventTypes.NotificationSend, entityId, 1);
        if (!string.IsNullOrWhiteSpace(idempotencySuffix))
        {
            idempotencyKey = $"{idempotencyKey}:{idempotencySuffix}";
        }

        await outbox.EnqueueAsync(OutboxEventTypes.NotificationSend, entityId, notification, idempotencyKey, ct);
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> values,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!values.TryGetValue(name, out var element))
            {
                continue;
            }

            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        return null;
    }

    private static Guid? ReadGuid(
        IReadOnlyDictionary<string, JsonElement> values,
        params string[] names)
    {
        var text = ReadString(values, names);
        return Guid.TryParse(text, out var id) && id != Guid.Empty ? id : null;
    }

    private async Task FailAsync(string action, string entityType, Guid entityId, Exception exception, CancellationToken ct)
    {
        await audit.LogAsync(user.UserId, action, entityType, entityId == Guid.Empty ? null : entityId.ToString(), new { error = exception.Message }, ct);
        throw exception;
    }

    private Task AuditAsync(string action, string entityType, Guid entityId, object? details, CancellationToken ct) =>
        audit.LogAsync(user.UserId, action, entityType, entityId.ToString(), details, ct);

    private static ValidationException Validation(string message) => new(message);
    private static NotFoundException NotFound(string code, string message) => new(message, code);
    private static ConflictException Conflict(string code, string message) => new(message, code);
    private static DomainException InvalidDecisionPayload() => new("The approved decision payload is missing or invalid.", "INVALID_DECISION_PAYLOAD");

    private async Task<DecisionRecord> DecisionRecordForAsync(Guid decisionId, CancellationToken ct)
    {
        var record = await db.DecisionRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == decisionId, ct)
            ?? throw new ForbiddenException("The referenced decision does not exist.", "DECISION_REQUIRED");
        if (record.Outcome is not (DecisionOutcome.Approved or DecisionOutcome.Modified)) throw new ForbiddenException("The decision is not an approval.", "DECISION_REQUIRED");
        if (string.IsNullOrWhiteSpace(record.FinalPayloadJson)) throw InvalidDecisionPayload();
        return record;
    }

    private static JsonElement ParsePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw InvalidDecisionPayload();
        try
        {
            var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw InvalidDecisionPayload();
        }
    }

    private static bool TryGuid(JsonElement element, string name, out Guid value)
    {
        value = Guid.Empty;
        // Check ValueKind first: GetString() throws on a number/object and would
        // leak a raw JsonException to the caller instead of fail-closed 403/400.
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Guid.TryParse(property.GetString(), out value) &&
               value != Guid.Empty;
    }

    private static bool TryDecimal(JsonElement element, string name, out decimal value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetDecimal(out value);
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out value);
    }

    private Guid? ScopedOrgId() => user.OrgId;

    private bool IsPrivileged() => user.IsInRole(RoleNames.SystemAdmin) || user.IsInRole(RoleNames.EmergencyCoordinator);

    private void ScopedWrite(Guid ownerOrgId)
    {
        var org = ScopedOrgId();
        if (!IsPrivileged() && org.HasValue && ownerOrgId != org.Value) throw new ForbiddenException("This record belongs to another organisation.", "ORG_SCOPE_VIOLATION");
    }

    private async Task ScopedRead(Guid inventoryId)
    {
        var org = ScopedOrgId();
        if (!org.HasValue || IsPrivileged()) return;
        var owner = await db.Inventories.AsNoTracking().Where(x => x.Id == inventoryId).Select(x => x.OrgId).FirstOrDefaultAsync();
        if (owner != Guid.Empty && owner != org.Value) throw new ForbiddenException("This inventory belongs to another organisation.", "ORG_SCOPE_VIOLATION");
    }

    private static int NormalizePage(int page, int pageSize, out int size)
    {
        var current = page < 1 ? 1 : page;
        size = pageSize < 1 ? 50 : Math.Min(pageSize, MaxPageSize);
        return (current - 1) * size;
    }

    private static bool FiniteCoordinate(double lat, double lng) =>
        double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;

    private static void ValidateCoordinates(double? lat, double? lng, bool allowNull)
    {
        if (allowNull && (!lat.HasValue || !lng.HasValue)) return;
        if (!lat.HasValue || !lng.HasValue || double.IsFinite(lat.Value) is false || double.IsFinite(lng.Value) is false ||
            lat.Value is < -90 or > 90 || lng.Value is < -180 or > 180)
            throw Validation("Coordinates must be finite and within latitude/longitude bounds.");
    }

    private static InventoryDto MapInventory(Inventory x) => new(x.Id, x.ResourceTypeId, x.OrgId, x.Quantity, x.Available,
        x.Reserved, x.Allocated, x.Dispatched, x.Utilised, x.ReorderThreshold, x.ExpiryDate, x.Lat, x.Lng,
        x.AccessibilityCapable, x.Available <= x.ReorderThreshold);

    private static AllocationDto MapAllocation(Allocation x) => new(x.Id, x.RequestId, x.InventoryId, x.Quantity, x.Status, x.DecisionId, x.CreatedAt);
    private static DispatchDto MapDispatch(Dispatch x) => new(x.Id, x.AllocationId, x.DecisionId, x.Quantity, x.Status, x.ConfirmedAt, x.ConfirmedBy);

    private static ShelterDto MapShelter(Shelter x) => new(x.Id, x.Name, x.Capacity, x.Occupancy, x.Capacity - x.Occupancy,
        x.Facilities, x.AccessibilityFeatures, x.Services, x.Lat, x.Lng, x.OrgId, x.Approved);

    private static VolunteerDto MapVolunteer(Volunteer x) => new(x.Id, x.UserId, x.SkillsJson, x.LanguagesJson, x.VerificationStatus, x.Active, x.Lat, x.Lng);
    private static VolunteerTaskDto MapTask(VolunteerTask x) => new(x.Id, x.RequestId, x.Title, x.RequiredSkillsJson, x.Lat, x.Lng, x.ScheduledAt, x.Status, x.VolunteerId, x.CompletedAt);
    private static AssistanceRequestDto MapAssistance(AssistanceRequest x) => new(x.Id, x.FromOrgId, x.ToOrgId, x.ResourceTypeId, x.Quantity, x.Reason, x.Status, x.ShelterId, x.ResponseReason, x.CreatedAt);

    // A pending row is actionable only when the request state machine permits
    // the exact Phase 3 transition. Seeded operational rows whose request never
    // reached Approved/Dispatched must not be offered as retryable work.
    private static bool NeedsTransition(RequestStatus current, RequestStatus target) =>
        current != target && RequestStateMachine.CanTransition(current, target);

    private async Task<RequestStatus> CurrentRequestStatusAsync(Guid requestId, CancellationToken ct) =>
        await db.Requests.AsNoTracking()
            .Where(x => x.Id == requestId)
            .Select(x => (RequestStatus?)x.Status)
            .FirstOrDefaultAsync(ct)
        ?? throw NotFound("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");

    private static void EnsureRetryable(RequestStatus current, RequestStatus target)
    {
        if (RequestStateMachine.CanTransition(current, target)) return;
        throw new ConflictException(
            $"Cannot retry request status transition from {current} to {target}.",
            "STATUS_TRANSITION_NOT_RETRYABLE");
    }

    private static bool HasAccessibilityNeed(RequestSummaryDto request) =>
        !string.IsNullOrWhiteSpace(request.AccessibilityNeeds) &&
        !request.AccessibilityNeeds.Contains("none", StringComparison.OrdinalIgnoreCase);

    private static decimal EstimateRequestedQuantity(RequestSummaryDto request)
    {
        var match = Regex.Match(request.Description ?? string.Empty, @"\b(\d+(?:\.\d+)?)\s*(?:packs?|kits?|litres?|units?|items?)?\b", RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed > 0) return parsed;
        return Math.Max(1, request.HouseholdSize);
    }

    private static bool MatchesCategory(string resourceTypeName, Category category)
    {
        var name = resourceTypeName.ToLowerInvariant();
        return category switch
        {
            Category.Food => name.Contains("food") || name.Contains("water") || name.Contains("nutrition"),
            Category.Medicine => name.Contains("medical") || name.Contains("medicine") || name.Contains("kit"),
            Category.Shelter => name.Contains("shelter") || name.Contains("tarp") || name.Contains("blanket"),
            Category.Transport => name.Contains("transport") || name.Contains("fuel") || name.Contains("vehicle"),
            Category.Accessibility => name.Contains("access") || name.Contains("wheelchair") || name.Contains("assist"),
            _ => true
        };
    }

    private static string NormalizeJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "[]";
        return IsValidJsonArray(value) ? value.Trim() : JsonSerializer.Serialize(new[] { value.Trim() });
    }

    private static bool IsValidJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { using var doc = JsonDocument.Parse(value); return doc.RootElement.ValueKind == JsonValueKind.Array; }
        catch (JsonException) { return false; }
    }

    private static string[] ParseArray(string? value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value ?? "[]");
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            return doc.RootElement.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray();
        }
        catch { return []; }
    }
}
