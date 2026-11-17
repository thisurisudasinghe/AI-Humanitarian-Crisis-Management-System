using Cms.Domain.Entities;

namespace Cms.Application.Interfaces;

public interface IOutbox
{
    void Enqueue(OutboxMessage message);
}