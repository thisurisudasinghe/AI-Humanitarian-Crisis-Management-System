using Cms.Domain.Entities;

namespace Cms.Application.Interfaces;

public interface IOutbox
{
    /// <summary>
    /// Adds the message to the caller's DbContext. The caller owns the transaction and
    /// is responsible for calling SaveChangesAsync.
    /// </summary>
    Task EnqueueAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);

    /// <summary>Backwards-compatible synchronous enqueue. Also does not save changes.</summary>
    void Enqueue(OutboxMessage message);
}