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

    /// <summary>
    /// Indicates that aggregates are provisional until the owning modules publish
    /// their final table and column names.
    /// </summary>
    public bool IsPlaceholder { get; set; } = true;
}
