using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;

namespace Cms.Application.Interfaces;

/// <summary>Read-only operations for the dead-letter queue (SystemAdmin).</summary>
public interface IOutboxAdminService
{
    Task<PagedResultDto<OutboxMessageDto>> GetDeadLetteredAsync(
        OutboxQueryDto query,
        CancellationToken cancellationToken = default);
}
