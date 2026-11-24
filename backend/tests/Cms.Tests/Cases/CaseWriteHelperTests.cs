using System.Text.Json;
using Cms.Application.DTOs.Notifications;
using Cms.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class CaseWriteHelperTests
{
    [Fact]
    public async Task Outbox_row_uses_entity_id_and_idempotency_key()
    {
        using var context = TestContext.Create();
        var date = new FixedDateTimeService(new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc));
        var writer = TestContext.CreateWriter(context, date);
        var entityId = Guid.NewGuid();
        var key = $"Request.Submitted:{entityId}:1";

        await writer.AddOutboxAsync(
            "Request.Submitted",
            entityId,
            new { requestId = entityId, category = "Food" },
            key,
            default);
        await context.SaveChangesAsync();

        var row = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.Equal(entityId, row.EntityId);
        Assert.Equal(key, row.IdempotencyKey);
        Assert.Equal("Request.Submitted", row.Type);
        Assert.Equal(OutboxMessageStatus.Pending, row.Status);
        Assert.Contains("requestId", row.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotencyKey", row.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Notification_payload_matches_the_shared_contract()
    {
        using var context = TestContext.Create();
        var date = new FixedDateTimeService(new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc));
        var writer = TestContext.CreateWriter(context, date);
        var requestId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        await writer.AddNotificationAsync(
            requestId,
            recipientId,
            "Request.StatusChanged",
            new { from = "Submitted", to = "Verified" },
            2,
            default);
        await context.SaveChangesAsync();

        var row = Assert.Single(await context.OutboxMessages.ToListAsync());
        var payload = JsonSerializer.Deserialize<NotificationPayloadDto>(
            row.Payload,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal(requestId, row.EntityId);
        Assert.Equal($"Notification.Send:{requestId}:2", row.IdempotencyKey);
        Assert.Equal(recipientId, payload!.RecipientUserId);
        Assert.Equal(requestId, payload.RequestId);
        Assert.Equal("Request.StatusChanged", payload.TemplateKey);
        Assert.Equal("en", payload.Language);
    }

    [Fact]
    public async Task Duplicate_idempotency_key_is_ignored_across_writer_instances()
    {
        using var context = TestContext.Create();
        var date = new FixedDateTimeService(new DateTime(2026, 11, 21, 9, 0, 0, DateTimeKind.Utc));
        var firstWriter = TestContext.CreateWriter(context, date);
        var secondWriter = TestContext.CreateWriter(context, date);
        var entityId = Guid.NewGuid();
        var key = $"Request.Submitted:{entityId}:1";

        await firstWriter.AddOutboxAsync("Request.Submitted", entityId, new { attempt = 1 }, key, default);
        await secondWriter.AddOutboxAsync("Request.Submitted", entityId, new { attempt = 2 }, key, default);
        await context.SaveChangesAsync();

        var row = Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.Equal(key, row.IdempotencyKey);
        Assert.Contains("attempt", row.Payload, StringComparison.Ordinal);
    }
}
