using Cms.Application.Common;
using Cms.Application.DTOs.Kpi;
using Cms.Application.Interfaces;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>
/// KPI provider. The identity, audit and donation sections are computed from the
/// schemas Dev 1 owns (<c>identity</c>, <c>common</c> and <c>don</c>). The
/// cross-module sections stay empty and are reported through
/// <see cref="KpiReportDto.PendingSections"/> until the cases, AI, resources,
/// shelter and notification owners publish their final table and column names,
/// so no other member's schema is guessed at.
/// </summary>
public sealed class KpiService : IKpiService
{
    private static readonly string[] LiveSections =
        ["identity", "donations", "notifications"];

    private static readonly string[] PendingModuleSections =
        ["cases", "resources", "shelter", "ai"];

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
            AvailableSections = LiveSections,
            PendingSections = PendingModuleSections,
            RequestsByArea = Array.Empty<KpiAreaBreakdownDto>(),
            SheltersByArea = Array.Empty<KpiAreaBreakdownDto>(),
            IsPlaceholder = true
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
