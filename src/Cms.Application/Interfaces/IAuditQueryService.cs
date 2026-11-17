using Cms.Application.DTOs;
using Cms.Application.DTOs.Audit;

namespace Cms.Application.Interfaces;

public interface IAuditQueryService
{
    Task<PagedResultDto<AuditEventDto>> QueryAsync(
        AuditEventQueryDto query,
        CancellationToken cancellationToken = default);
}
