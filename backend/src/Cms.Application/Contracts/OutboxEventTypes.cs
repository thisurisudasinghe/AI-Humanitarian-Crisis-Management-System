namespace Cms.Application.Contracts;

/// <summary>Frozen outbox event types and idempotency-key format (Block S).</summary>
public static class OutboxEventTypes
{
    public const string RequestSubmitted = "Request.Submitted";
    public const string NotificationSend = "Notification.Send";

    public static string BuildIdempotencyKey(string type, Guid entityId, int version) =>
        $"{type}:{entityId}:{version}";
}
