using Cms.Application.Common;
using Cms.Application.DTOs.Kpi;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>
/// KPI provider. All report sections are computed from the canonical SQL schema:
/// identity/audit/notifications, donations, cases, AI governance, resources and
/// shelters. A null metric means "no denominator in the selected period", while
/// zero is a real measured value.
/// </summary>
public sealed class KpiService : IKpiService
{
    private static readonly string[] LiveSections =
        ["identity", "donations", "notifications", "cases", "resources", "shelter", "ai"];

    private static readonly string[] PendingModuleSections = [];

    private readonly CmsDbContext _db;
    private readonly IDonationService _donationService;
    private readonly IDateTimeService _dateTimeService;

    public KpiService(
        CmsDbContext db,
        IDonationService donationService,
        IDateTimeService dateTimeService)
    {
        _db = db;
        _donationService = donationService;
        _dateTimeService = dateTimeService;
    }

    public async Task<KpiReportDto> GetReportAsync(
        DateTime from,
        DateTime to,
        Guid? crisisEventId,
        Guid? areaId,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ValidationException(
                "The end of the reporting period must be after its start.",
                new Dictionary<string, string[]>
                {
                    ["to"] = ["Must be greater than or equal to from."]
                });
        }

        var fromUtc = AsUtc(from);
        var toUtc = AsUtc(to);

        var totalUserCount = await _db.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeUserCount = await _db.Users
            .AsNoTracking()
            .CountAsync(user => user.Status == UserStatus.Active, cancellationToken);

        var newUserCount = await _db.Users
            .AsNoTracking()
            .CountAsync(
                user => user.CreatedAt >= fromUtc && user.CreatedAt <= toUtc,
                cancellationToken);

        var usersByRole = await _db.UserRoles
            .AsNoTracking()
            .GroupBy(userRole => userRole.Role.Name)
            .Select(group => new KpiRoleCountDto
            {
                Role = group.Key,
                Count = group.Count()
            })
            .OrderBy(item => item.Role)
            .ToListAsync(cancellationToken);

        var auditEventCount = await _db.AuditEvents
            .AsNoTracking()
            .CountAsync(
                auditEvent =>
                    auditEvent.CreatedAt >= fromUtc && auditEvent.CreatedAt <= toUtc,
                cancellationToken);

        var activeCampaignCount = await _db.Campaigns
            .AsNoTracking()
            .CountAsync(
                campaign => campaign.Status == CampaignStatus.Active,
                cancellationToken);

        var totalNotificationCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var sentNotificationCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.Status == NotificationStatus.Sent,
                cancellationToken);

        var failedNotificationCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.Status == NotificationStatus.Failed,
                cancellationToken);

        var deadLetterCount = await _db.OutboxMessages
            .AsNoTracking()
            .CountAsync(
                message => message.Status == OutboxMessageStatus.DeadLettered,
                cancellationToken);

        // Reuse the donation report so KPI totals follow exactly the same
        // currency grouping and cancellation rules as the transparency report.
        var donationReport = await _donationService.GetReportAsync(
            fromUtc,
            toUtc,
            cancellationToken);

        // --- Cases: currently unresolved requests and first human response ---
        var openStatuses = Enum.GetValues<RequestStatus>()
            .Where(RequestStateMachine.IsOpen)
            .ToArray();

        var scopedRequests = _db.Requests
            .AsNoTracking()
            .Where(request =>
                (!crisisEventId.HasValue || request.CrisisId == crisisEventId.Value) &&
                (!areaId.HasValue || request.AreaId == areaId.Value));

        var unresolvedRequestCount = await scopedRequests
            .CountAsync(request => openStatuses.Contains(request.Status), cancellationToken);

        var responseSamples = await scopedRequests
            .Where(request =>
                request.VerifiedAt != null &&
                request.SubmittedAt >= fromUtc &&
                request.SubmittedAt <= toUtc)
            .Select(request => new
            {
                request.SubmittedAt,
                VerifiedAt = request.VerifiedAt!.Value
            })
            .ToListAsync(cancellationToken);

        double? averageResponseTimeHours = responseSamples.Count == 0
            ? null
            : Math.Round(
                responseSamples.Average(sample => (sample.VerifiedAt - sample.SubmittedAt).TotalHours),
                2,
                MidpointRounding.AwayFromZero);

        // --- Resources: current stock buckets, optionally scoped by linked requests ---
        var inventories = _db.Inventories.AsNoTracking().AsQueryable();
        if (crisisEventId.HasValue || areaId.HasValue)
        {
            var linkedInventoryIds = await (from allocation in _db.Allocations.AsNoTracking()
                                            join request in scopedRequests
                                                on allocation.RequestId equals request.Id
                                            select allocation.InventoryId)
                .Distinct()
                .ToListAsync(cancellationToken);
            inventories = inventories.Where(inventory => linkedInventoryIds.Contains(inventory.Id));
        }

        var inventoryRows = await inventories
            .Select(inventory => new
            {
                inventory.Quantity,
                inventory.Utilised,
                inventory.Reserved,
                inventory.Dispatched
            })
            .ToListAsync(cancellationToken);

        var totalQuantity = inventoryRows.Sum(row => row.Quantity);
        var totalUtilised = inventoryRows.Sum(row => row.Utilised);
        double? resourceUtilisationPercent = totalQuantity > 0
            ? Math.Round((double)(totalUtilised / totalQuantity) * 100, 2, MidpointRounding.AwayFromZero)
            : null;
        int? reservedResources = inventoryRows.Count == 0
            ? null
            : (int)Math.Round(inventoryRows.Sum(row => row.Reserved), MidpointRounding.AwayFromZero);
        int? dispatchedResources = inventoryRows.Count == 0
            ? null
            : (int)Math.Round(inventoryRows.Sum(row => row.Dispatched), MidpointRounding.AwayFromZero);

        // --- Shelter: approved capacity/occupancy, optionally scoped by linked requests ---
        var shelters = _db.Shelters.AsNoTracking().Where(shelter => shelter.Approved);
        if (crisisEventId.HasValue || areaId.HasValue)
        {
            var linkedShelterIds = await (from allocation in _db.ShelterAllocations.AsNoTracking()
                                          join request in scopedRequests
                                              on allocation.RequestId equals request.Id
                                          select allocation.ShelterId)
                .Distinct()
                .ToListAsync(cancellationToken);
            shelters = shelters.Where(shelter => linkedShelterIds.Contains(shelter.Id));
        }

        var shelterRows = await shelters
            .Select(shelter => new { shelter.Capacity, shelter.Occupancy })
            .ToListAsync(cancellationToken);

        var totalCapacity = shelterRows.Sum(row => row.Capacity);
        var totalOccupancy = shelterRows.Sum(row => row.Occupancy);
        double? shelterOccupancyPercent = totalCapacity > 0
            ? Math.Round((double)totalOccupancy / totalCapacity * 100, 2, MidpointRounding.AwayFromZero)
            : null;
        int? sheltersAtCapacity = shelterRows.Count == 0
            ? null
            : shelterRows.Count(row => row.Occupancy >= row.Capacity);

        // --- AI governance: Modified or Rejected decisions count as overrides ---
        var decisionQuery = from decision in _db.DecisionRecords.AsNoTracking()
                            join recommendation in _db.Recommendations.AsNoTracking()
                                on decision.RecommendationId equals recommendation.Id
                            where decision.DecidedAt >= fromUtc && decision.DecidedAt <= toUtc
                            select new { decision, recommendation };

        if (crisisEventId.HasValue || areaId.HasValue)
        {
            decisionQuery = from item in decisionQuery
                            join request in _db.Requests.AsNoTracking()
                                on item.recommendation.TargetId equals request.Id into linkedRequests
                            from request in linkedRequests.DefaultIfEmpty()
                            where
                                (!crisisEventId.HasValue ||
                                 request.CrisisId == crisisEventId.Value ||
                                 item.recommendation.TargetId == crisisEventId.Value) &&
                                (!areaId.HasValue || request.AreaId == areaId.Value)
                            select item;
        }

        var decisionOutcomes = await decisionQuery
            .Select(item => item.decision.Outcome)
            .ToListAsync(cancellationToken);
        var overrideCount = decisionOutcomes.Count(outcome =>
            outcome == DecisionOutcome.Modified || outcome == DecisionOutcome.Rejected);
        double? aiOverrideRatePercent = decisionOutcomes.Count == 0
            ? null
            : Math.Round((double)overrideCount / decisionOutcomes.Count * 100, 2, MidpointRounding.AwayFromZero);

        // --- Mapping: requests and allocated shelters grouped by operational area ---
        var areaRows = _db.Set<Area>().AsNoTracking();
        var periodRequests = scopedRequests.Where(request =>
            request.SubmittedAt >= fromUtc && request.SubmittedAt <= toUtc);

        var requestAreaRows = await (from area in areaRows
                                     join request in periodRequests
                                         on area.Id equals request.AreaId
                                     select new { area.Id, area.Name })
            .ToListAsync(cancellationToken);
        var requestsByArea = requestAreaRows
            .GroupBy(row => new { row.Id, row.Name })
            .Select(group => new KpiAreaBreakdownDto
            {
                AreaId = group.Key.Id,
                AreaName = group.Key.Name,
                Count = group.Count()
            })
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.AreaName)
            .ToList();

        var shelterAreaRows = await (from allocation in _db.ShelterAllocations.AsNoTracking()
                                     join request in periodRequests
                                         on allocation.RequestId equals request.Id
                                     join area in areaRows
                                         on request.AreaId equals area.Id
                                     select new { area.Id, area.Name, allocation.ShelterId })
            .ToListAsync(cancellationToken);
        var sheltersByArea = shelterAreaRows
            .GroupBy(row => new { row.Id, row.Name })
            .Select(group => new KpiAreaBreakdownDto
            {
                AreaId = group.Key.Id,
                AreaName = group.Key.Name,
                Count = group.Select(row => row.ShelterId).Distinct().Count()
            })
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.AreaName)
            .ToList();

        return new KpiReportDto
        {
            From = fromUtc,
            To = toUtc,
            CrisisEventId = crisisEventId,
            AreaId = areaId,
            GeneratedAt = _dateTimeService.UtcNow,
            TotalUserCount = totalUserCount,
            ActiveUserCount = activeUserCount,
            NewUserCount = newUserCount,
            UsersByRole = usersByRole,
            AuditEventCount = auditEventCount,
            ActiveCampaignCount = activeCampaignCount,
            TotalNotificationCount = totalNotificationCount,
            SentNotificationCount = sentNotificationCount,
            FailedNotifications = failedNotificationCount,
            DeadLetterCount = deadLetterCount,
            DonationTotals = donationReport.CurrencyTotals,
            SuspiciousDonationCount = donationReport.SuspiciousDonationCount,
            SuspiciousAllocationCount = donationReport.SuspiciousAllocationCount,
            UnresolvedRequestCount = unresolvedRequestCount,
            AverageResponseTimeHours = averageResponseTimeHours,
            ResourceUtilisationPercent = resourceUtilisationPercent,
            ReservedResources = reservedResources,
            DispatchedResources = dispatchedResources,
            ShelterOccupancyPercent = shelterOccupancyPercent,
            SheltersAtCapacity = sheltersAtCapacity,
            AiOverrideRatePercent = aiOverrideRatePercent,
            AvailableSections = LiveSections,
            PendingSections = PendingModuleSections,
            RequestsByArea = requestsByArea,
            SheltersByArea = sheltersByArea,
            IsPlaceholder = false
        };
    }

    private static DateTime AsUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
