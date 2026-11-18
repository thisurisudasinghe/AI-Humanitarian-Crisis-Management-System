
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Services.Outbox;

public class OutboxService : IOutbox
{
    private readonly ApplicationDbContext _context;

    public OutboxService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task EnqueueAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Intentionally no SaveChangesAsync here: the outbox row must be persisted
        // in the same transaction as the business change that produced it.
        _context.OutboxMessages.Add(message);
        return Task.CompletedTask;
    }

    public void Enqueue(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
    }
}
