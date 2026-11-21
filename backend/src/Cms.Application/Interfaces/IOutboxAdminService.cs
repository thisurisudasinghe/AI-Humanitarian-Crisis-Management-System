using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;

namespace Cms.Application.Interfaces;

/// <summary>Read-only operations for the dead-letter queue (SystemAdmin).</summary>
public interface IOutboxAdminService
{
    Task<PagedResultDto<OutboxMessageDto>> GetDeadLetteredAsync(
        OutboxQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requeues a dead-lettered message for another delivery attempt.
    /// Returns false when no dead-lettered message has that id.
    /// </summary>
    Task<bool> RetryDeadLetteredAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
