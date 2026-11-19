namespace Cms.Application.Contracts;

/// <summary>
/// Transactional outbox. Implemented by Dev 1 (shared foundation).
/// The row is added to the caller's DbContext and committed by the caller's
/// SaveChangesAsync, so the business change and the event are atomic.
/// Enqueue is idempotent: a duplicate idempotency key is silently ignored when
/// it is already tracked or persisted. A concurrent duplicate is still stopped
/// by the unique <c>UX_outbox_idempotency_key</c> index.
/// <paramref name="payload"/> is serialized once with web JSON options
/// (camelCase, string enums); pass an object, not a pre-serialized JSON string.
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
