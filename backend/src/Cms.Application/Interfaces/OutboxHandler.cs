using Cms.Domain.Entities;

namespace Cms.Application.Interfaces;

public interface IOutboxHandler
{
    /// <summary>Frozen outbox event type handled by this implementation.</summary>
    string MessageType => string.Empty;

    Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}

public interface IOutboxHandlerRegistry
{
    IOutboxHandler GetHandler(string messageType);
}