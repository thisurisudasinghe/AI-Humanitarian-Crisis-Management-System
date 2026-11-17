using Cms.Application.DTOs.Kpi;
using Cms.Application.Interfaces;
using Cms.Application.Common;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Placeholder KPI provider. The SQL aggregates depend on the final table and
/// column names from the cases, AI, resources, shelter and notifications modules.
/// Those names are not published yet, so this returns a typed, stable contract
/// instead of guessing at another member's schema.
/// </summary>
public sealed class KpiService : IKpiService
{
    private readonly IDateTimeService _dateTimeService;

    public KpiService(IDateTimeService dateTimeService)
    {
        _dateTimeService = dateTimeService;
    }

    public Task<KpiReportDto> GetReportAsync(
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

        return Task.FromResult(new KpiReportDto
        {
            From = from,
            To = to,
            CrisisEventId = crisisEventId,
            AreaId = areaId,
            GeneratedAt = _dateTimeService.UtcNow,
            IsPlaceholder = true
        });
    }
}
