namespace Cms.Application.Contracts;

/// <summary>
/// Transactional outbox. Implemented by Member 3 (M9).
/// The row is added to the caller's DbContext and committed by the caller's SaveChangesAsync.
/// </summary>
public interface IOutbox
{
    Task EnqueueAsync(
        string type,
        Guid entityId,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
