using System.Text.Json;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Infrastructure;

public class OutboxServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private static CmsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CmsDbContext>()
            .UseInMemoryDatabase($"cms-outbox-writer-{Guid.NewGuid()}")
            .Options);

    private static OutboxService CreateService(CmsDbContext context) =>
        new(context, new FixedDateTimeService(FixedNow));

    [Fact]
    public async Task Enqueue_adds_a_pending_row_without_saving()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var entityId = Guid.NewGuid();

        await service.EnqueueAsync(
            "Request.Submitted",
            entityId,
            new { requestId = entityId, urgency = Urgency.High },
            "Request.Submitted:key:1");

        var message = Assert.Single(context.OutboxMessages.Local);
        Assert.Equal("Request.Submitted", message.Type);
        Assert.Equal(entityId, message.EntityId);
        Assert.Equal("Request.Submitted:key:1", message.IdempotencyKey);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.RetryCount);
        Assert.Equal(FixedNow, message.CreatedAt);
        Assert.Equal(FixedNow, message.UpdatedAt);
        Assert.Null(message.NextRetryAt);
        Assert.Null(message.ProcessedAt);

        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(entityId, payload.RootElement.GetProperty("requestId").GetGuid());
        Assert.Equal("High", payload.RootElement.GetProperty("urgency").GetString());

        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Enqueue_ignores_a_duplicate_key_that_is_already_tracked()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), new { version = 1 }, "dup-key");
        await service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), new { version = 2 }, "dup-key");

        Assert.Single(context.OutboxMessages.Local);
    }

    [Fact]
    public async Task Enqueue_ignores_a_duplicate_key_that_is_already_persisted()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), new { version = 1 }, "dup-key");
        await context.SaveChangesAsync();

        await service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), new { version = 2 }, "dup-key");

        Assert.Single(context.OutboxMessages.Local);
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Enqueue_trims_type_and_key()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.EnqueueAsync("  Request.Submitted  ", Guid.NewGuid(), new { }, "  key  ");

        var message = Assert.Single(context.OutboxMessages.Local);
        Assert.Equal("Request.Submitted", message.Type);
        Assert.Equal("key", message.IdempotencyKey);
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("   ", "key")]
    [InlineData("Request.Submitted", "")]
    [InlineData("Request.Submitted", "   ")]
    public async Task Enqueue_rejects_missing_type_or_key(string type, string key)
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EnqueueAsync(type, Guid.NewGuid(), new { }, key));
    }

    [Fact]
    public async Task Enqueue_rejects_an_empty_entity_id()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EnqueueAsync("Request.Submitted", Guid.Empty, new { }, "key"));
    }

    [Fact]
    public async Task Enqueue_rejects_a_null_payload()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), null!, "key"));
    }

    [Fact]
    public async Task Enqueue_rejects_values_over_the_column_limits()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EnqueueAsync(new string('t', 101), Guid.NewGuid(), new { }, "key"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.EnqueueAsync("Request.Submitted", Guid.NewGuid(), new { }, new string('k', 257)));
    }

    private sealed class FixedDateTimeService : IDateTimeService
    {
        public FixedDateTimeService(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
