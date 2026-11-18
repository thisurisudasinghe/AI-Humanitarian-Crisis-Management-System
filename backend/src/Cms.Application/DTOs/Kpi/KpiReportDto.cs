using Cms.Application.DTOs.Donations;

namespace Cms.Application.DTOs.Kpi;

public class KpiReportDto
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public Guid? CrisisEventId { get; set; }

    public Guid? AreaId { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int UnresolvedRequestCount { get; set; }

    public double? AverageResponseTimeHours { get; set; }

    public double? ResourceUtilisationPercent { get; set; }

    public int? ReservedResources { get; set; }

    public int? DispatchedResources { get; set; }

    public double? ShelterOccupancyPercent { get; set; }

    public int? SheltersAtCapacity { get; set; }

    public double? AiOverrideRatePercent { get; set; }

    public int? FailedNotifications { get; set; }

    public int? DeadLetterCount { get; set; }

    // -----------------------------------------------------------------------
    // Dev 1 owned sections (identity, common and don schemas).
    // These are organisation-wide totals and are not affected by the optional
    // crisisEventId/areaId filters, because users and donations are not scoped
    // to an operational area.
    // -----------------------------------------------------------------------

    /// <summary>Total number of user accounts.</summary>
    public int TotalUserCount { get; set; }

    /// <summary>Number of accounts whose status is <c>Active</c>.</summary>
    public int ActiveUserCount { get; set; }

    /// <summary>Accounts created within the reporting period.</summary>
    public int NewUserCount { get; set; }

    /// <summary>Account count per frozen role name.</summary>
    public IReadOnlyCollection<KpiRoleCountDto> UsersByRole { get; set; } =
        Array.Empty<KpiRoleCountDto>();

    /// <summary>Audit events recorded within the reporting period.</summary>
    public int AuditEventCount { get; set; }

    /// <summary>Campaigns currently in the <c>Active</c> state.</summary>
    public int ActiveCampaignCount { get; set; }

    /// <summary>
    /// Donation totals for the period, grouped by currency so values in
    /// different currencies are never added together.
    /// </summary>
    public IReadOnlyCollection<DonationCurrencyTotalDto> DonationTotals { get; set; } =
        Array.Empty<DonationCurrencyTotalDto>();

    /// <summary>Donations flagged for human review within the period.</summary>
    public int SuspiciousDonationCount { get; set; }

    /// <summary>Allocations flagged for human review within the period.</summary>
    public int SuspiciousAllocationCount { get; set; }

    /// <summary>Sections backed by live aggregates in this build.</summary>
    public IReadOnlyCollection<string> AvailableSections { get; set; } =
        Array.Empty<string>();

    /// <summary>Sections still waiting on another module's published schema.</summary>
    public IReadOnlyCollection<string> PendingSections { get; set; } =
        Array.Empty<string>();

    /// <summary>Requests per area (FR-MAP-05, P2). Empty until cases/geo schemas are published.</summary>
    public IReadOnlyCollection<KpiAreaBreakdownDto> RequestsByArea { get; set; } =
        Array.Empty<KpiAreaBreakdownDto>();

    /// <summary>Shelters per area (FR-MAP-05, P2). Empty until shelter/geo schemas are published.</summary>
    public IReadOnlyCollection<KpiAreaBreakdownDto> SheltersByArea { get; set; } =
        Array.Empty<KpiAreaBreakdownDto>();

    /// <summary>
    /// Indicates that at least one section is provisional until the owning module
    /// publishes its final table and column names.
    /// </summary>
    public bool IsPlaceholder { get; set; } = true;
}

/// <summary>One row of an area breakdown table on the KPI page.</summary>
public class KpiAreaBreakdownDto
{
    public Guid AreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    public int Count { get; set; }
}

/// <summary>Account count for a single frozen role.</summary>
public class KpiRoleCountDto
{
    public string Role { get; set; } = string.Empty;

    public int Count { get; set; }
}
