
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

    public void Enqueue(OutboxMessage message)
    {
        _context.OutboxMessages.Add(message);
    }
}
