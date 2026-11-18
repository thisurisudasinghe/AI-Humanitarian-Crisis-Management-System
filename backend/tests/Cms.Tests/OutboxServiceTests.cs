using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Infrastructure.Services.Outbox;

namespace Cms.Tests;

public class OutboxServiceTests
{
    [Fact]
    public async Task EnqueueAsync_AddsToCallerContext_WithoutSaving()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var context = TestDb.Create(databaseName);
        using var observer = TestDb.Create(databaseName);

        var service = new OutboxService(context);

        await service.EnqueueAsync(new OutboxMessage
        {
            Type = OutboxTypes.NotificationSend,
            Payload = "{}",
            EntityId = Guid.NewGuid(),
            IdempotencyKey = "test-key"
        });

        Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());

        // The caller still owns the transaction: no SaveChangesAsync in EnqueueAsync.
        Assert.Empty(observer.OutboxMessages);
    }

    [Fact]
    public async Task EnqueueAsync_WithCancelledToken_DoesNotEnqueue()
    {
        using var context = TestDb.Create();
        var service = new OutboxService(context);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.EnqueueAsync(
                new OutboxMessage { IdempotencyKey = "cancelled" },
                cts.Token));

        Assert.Empty(context.ChangeTracker.Entries<OutboxMessage>());
    }
}
