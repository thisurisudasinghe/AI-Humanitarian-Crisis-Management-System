using Cms.Application.DTOs.Kpi;

namespace Cms.Application.Interfaces;

public interface IKpiService
{
    Task<KpiReportDto> GetReportAsync(
        DateTime from,
        DateTime to,
        Guid? crisisEventId,
        Guid? areaId,
        CancellationToken cancellationToken = default);
}
