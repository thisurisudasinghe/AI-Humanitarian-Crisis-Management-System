using Cms.Application.Interfaces;

namespace Cms.Infrastructure.Services.Outbox;

public class OutboxHandlerRegistry : IOutboxHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IOutboxHandler> _handlers;

    public OutboxHandlerRegistry(IEnumerable<IOutboxHandler> handlers)
    {
        _handlers = handlers
            .Where(h => !string.IsNullOrWhiteSpace(h.MessageType))
            .GroupBy(h => h.MessageType, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }

    public IOutboxHandler GetHandler(string messageType)
    {
        if (_handlers.TryGetValue(messageType, out var handler))
        {
            return handler;
        }

        throw new NotSupportedException(
            $"No outbox handler is registered for message type '{messageType}'.");
    }
}
