using Cms.Domain.Entities;

namespace Cms.Application.Contracts;

/// <summary>
/// Handles one frozen outbox event type. Implemented by Member 3; any module may register handlers.
/// </summary>
public interface IOutboxHandler
{
    string Type { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
