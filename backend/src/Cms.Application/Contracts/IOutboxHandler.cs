using Cms.Domain.Entities;

namespace Cms.Application.Contracts;

/// <summary>
/// Handles one frozen outbox event type. Any module may register a handler; the
/// Dev 1 outbox processor dispatches due rows to the handler whose <see cref="Type"/>
/// matches. Member 3 (M9) owns the <c>Notification.Send</c> handler.
/// </summary>
public interface IOutboxHandler
{
    string Type { get; }

    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
