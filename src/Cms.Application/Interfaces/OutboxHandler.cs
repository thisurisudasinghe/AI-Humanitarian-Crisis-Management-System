using Cms.Domain.Entities;

namespace Cms.Application.Interfaces;

public interface IOutboxHandler
{
    Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default);
}