using Cms.Application.Contracts;
using Cms.Domain.Entities;

namespace Cms.Infrastructure.Stubs;

/// <summary>Temporary outbox. Member 3 replaces this with the transactional outbox.</summary>
public sealed class NullOutbox : IOutbox
{
    public Task EnqueueAsync(
        string type,
        Guid entityId,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Temporary outbox handler. Member 3 registers the real handlers.</summary>
public sealed class NullOutboxHandler : IOutboxHandler
{
    public string Type => string.Empty;

    public Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
