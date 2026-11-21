using System.Text.Json;
using Cms.Application.Cases.Common;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class CaseWriteHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CmsDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly CasesOptions _options;
    private readonly IOutbox _outbox;

    public CaseWriteHelper(
        CmsDbContext context,
        IDateTimeService dateTimeService,
        CasesOptions options,
        IOutbox outbox)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _options = options;
        _outbox = outbox;
    }

    public async Task<int> NextRequestVersionAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var count = await _context.Set<Domain.Entities.Cases.RequestStatusHistory>()
            .CountAsync(h => h.RequestId == requestId, cancellationToken);
        return count + 1;
    }

    public async Task AddHistoryAsync(
        Domain.Entities.Cases.RequestStatusHistory history,
        CancellationToken cancellationToken)
    {
        await _context.Set<Domain.Entities.Cases.RequestStatusHistory>().AddAsync(history, cancellationToken);
    }

    public async Task AddAuditAsync(
        string action,
        string entityType,
        Guid entityId,
        object? details,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var audit = new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = _dateTimeService.UtcNow
        };

        await _context.AuditEvents.AddAsync(audit, cancellationToken);
    }

    public Task AddOutboxAsync(
        string type,
        Guid entityId,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Use the shared transactional writer so EntityId, IdempotencyKey and the
        // payload shape match the common.outbox_messages contract and the
        // registered Notification.Send handler.
        return _outbox.EnqueueAsync(type, entityId, payload, idempotencyKey, cancellationToken);
    }

    public Task AddNotificationAsync(
        Guid requestId,
        Guid recipientUserId,
        string templateKey,
        object data,
        int version,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            RecipientUserId = recipientUserId,
            RequestId = requestId,
            TemplateKey = templateKey,
            Language = _options.DefaultLanguage,
            Data = data
        };

        return AddOutboxAsync(
            "Notification.Send",
            requestId,
            payload,
            $"Notification.Send:{requestId}:{version}",
            cancellationToken);
    }
}
