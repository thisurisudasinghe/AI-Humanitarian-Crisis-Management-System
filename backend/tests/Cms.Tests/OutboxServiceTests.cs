using Cms.Application.Contracts;
using Cms.Domain.Entities;
using Cms.Infrastructure.Services;

namespace Cms.Tests;

public class OutboxServiceTests
{
    [Fact]
    public async Task EnqueueAsync_AddsToCallerContext_WithoutSaving()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var context = TestDb.Create(databaseName);
        using var observer = TestDb.Create(databaseName);

        var service = new OutboxService(context, new DateTimeService());
        var entityId = Guid.NewGuid();

        await service.EnqueueAsync(
            OutboxEventTypes.NotificationSend,
            entityId,
            new { requestId = entityId },
            "test-key");

        Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());

        // The caller still owns the transaction: no SaveChangesAsync in EnqueueAsync.
        Assert.Empty(observer.OutboxMessages);
    }

    [Fact]
    public async Task EnqueueAsync_WithCancelledToken_DoesNotEnqueue()
    {
        using var context = TestDb.Create();
        var service = new OutboxService(context, new DateTimeService());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.EnqueueAsync(
                OutboxEventTypes.NotificationSend,
                Guid.NewGuid(),
                new { },
                "cancelled",
                cts.Token));

        Assert.Empty(context.ChangeTracker.Entries<OutboxMessage>());
    }
}
