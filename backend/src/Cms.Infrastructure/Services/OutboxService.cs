using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Transactional outbox writer. Adds a row to the caller's <see cref="CmsDbContext"/>
/// without saving, so the business change and the event commit atomically.
/// </summary>
public sealed class OutboxService : IOutbox
{
    public const int MaxTypeLength = 100;
    public const int MaxIdempotencyKeyLength = 256;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly CmsDbContext _db;
    private readonly IDateTimeService _dateTimeService;

    public OutboxService(CmsDbContext db, IDateTimeService dateTimeService)
    {
        _db = db;
        _dateTimeService = dateTimeService;
    }

    public async Task EnqueueAsync(
        string type,
        Guid entityId,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("An outbox event type is required.", nameof(type));
        }

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("An outbox entity id is required.", nameof(entityId));
        }

        ArgumentNullException.ThrowIfNull(payload);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("An outbox idempotency key is required.", nameof(idempotencyKey));
        }

        var normalizedType = type.Trim();
        var normalizedKey = idempotencyKey.Trim();

        if (normalizedType.Length > MaxTypeLength)
        {
            throw new ArgumentException(
                $"An outbox event type cannot exceed {MaxTypeLength} characters.",
                nameof(type));
        }

        if (normalizedKey.Length > MaxIdempotencyKeyLength)
        {
            throw new ArgumentException(
                $"An outbox idempotency key cannot exceed {MaxIdempotencyKeyLength} characters.",
                nameof(idempotencyKey));
        }

        var alreadyTracked = _db.OutboxMessages
            .Local
            .Any(message => string.Equals(
                message.IdempotencyKey,
                normalizedKey,
                StringComparison.Ordinal));

        if (alreadyTracked)
        {
            return;
        }

        var alreadyPersisted = await _db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(message => message.IdempotencyKey == normalizedKey, cancellationToken);

        if (alreadyPersisted)
        {
            return;
        }

        var now = _dateTimeService.UtcNow;

        var outboxMessage = new OutboxMessage
        {
            Type = normalizedType,
            EntityId = entityId,
            Payload = JsonSerializer.Serialize(payload, payload.GetType(), SerializerOptions),
            IdempotencyKey = normalizedKey,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            NextRetryAt = null,
            ProcessedAt = null,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
