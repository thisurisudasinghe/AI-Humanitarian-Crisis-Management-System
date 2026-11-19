using System.Globalization;
using System.Text;
using Cms.Application.DTOs.Kpi;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>Reporting and KPI endpoint (M11).</summary>
[ApiController]
[Route("api/v1/kpis")]
[Route("api/v1/reports/kpis")]
[Authorize(Policy = "KpiViewer")]
[Produces("application/json")]
public sealed class KpiController : ControllerBase
{
    private readonly IKpiService _kpiService;

    public KpiController(IKpiService kpiService)
    {
        _kpiService = kpiService;
    }

    /// <summary>Returns KPI aggregates for the supplied UTC period.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(KpiReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<KpiReportDto>> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? crisisEventId = null,
        [FromQuery] Guid? areaId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _kpiService.GetReportAsync(
            from,
            to,
            crisisEventId,
            areaId,
            cancellationToken));
    }

    /// <summary>Exports the same KPI aggregates as a UTF-8 CSV attachment.</summary>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? crisisEventId = null,
        [FromQuery] Guid? areaId = null,
        CancellationToken cancellationToken = default)
    {
        var report = await _kpiService.GetReportAsync(
            from,
            to,
            crisisEventId,
            areaId,
            cancellationToken);

        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"kpi-report-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.csv");

        return File(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(BuildCsv(report)),
            "text/csv",
            fileName);
    }

    private static string BuildCsv(KpiReportDto report)
    {
        var csv = new StringBuilder();

        csv.AppendLine("Metric,Value");
        Add("Period from", report.From.ToString("o", CultureInfo.InvariantCulture));
        Add("Period to", report.To.ToString("o", CultureInfo.InvariantCulture));
        Add("Crisis event id", report.CrisisEventId);
        Add("Area id", report.AreaId);
        Add("Generated at", report.GeneratedAt.ToString("o", CultureInfo.InvariantCulture));
        Add("Unresolved requests", report.UnresolvedRequestCount);
        Add("Average response time hours", report.AverageResponseTimeHours);
        Add("Resource utilisation percent", report.ResourceUtilisationPercent);
        Add("Reserved resources", report.ReservedResources);
        Add("Dispatched resources", report.DispatchedResources);
        Add("Shelter occupancy percent", report.ShelterOccupancyPercent);
        Add("Shelters at capacity", report.SheltersAtCapacity);
        Add("AI override rate percent", report.AiOverrideRatePercent);
        Add("Total notifications", report.TotalNotificationCount);
        Add("Sent notifications", report.SentNotificationCount);
        Add("Failed notifications", report.FailedNotifications);
        Add("Dead letters", report.DeadLetterCount);
        Add("Total users", report.TotalUserCount);
        Add("Active users", report.ActiveUserCount);
        Add("New users", report.NewUserCount);
        Add("Audit events", report.AuditEventCount);
        Add("Active campaigns", report.ActiveCampaignCount);
        Add("Suspicious donations", report.SuspiciousDonationCount);
        Add("Suspicious allocations", report.SuspiciousAllocationCount);

        foreach (var role in report.UsersByRole)
        {
            Add($"Users with role {role.Role}", role.Count);
        }

        foreach (var total in report.DonationTotals)
        {
            Add($"Donations {total.Currency} monetary total", total.MonetaryTotal);
            Add($"Donations {total.Currency} material estimated total", total.MaterialEstimatedTotal);
            Add($"Donations {total.Currency} allocated total", total.AllocatedTotal);
            Add($"Donations {total.Currency} spent total", total.SpentTotal);
            Add($"Donations {total.Currency} count", total.DonationCount);
            Add($"Donors {total.Currency}", total.DonorCount);
        }

        Add("Available sections", string.Join("; ", report.AvailableSections));
        Add("Pending sections", string.Join("; ", report.PendingSections));

        return csv.ToString();

        void Add(string metric, object? value)
        {
            csv.Append(Escape(metric))
                .Append(',')
                .AppendLine(Escape(Format(value)));
        }
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime dateTime => dateTime.ToString("o", CultureInfo.InvariantCulture),
        Guid guid => guid.ToString(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value)
    {
        if (!value.Contains(',') &&
            !value.Contains('"') &&
            !value.Contains('\r') &&
            !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
