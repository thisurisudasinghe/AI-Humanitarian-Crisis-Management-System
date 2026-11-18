using System.Text.Json;
using Cms.Application.Cases.Common;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class CaseWriteHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HashSet<string> _pendingOutboxKeys = new(StringComparer.Ordinal);
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;
    private readonly CasesOptions _options;

    public CaseWriteHelper(
        ApplicationDbContext context,
        IDateTimeService dateTimeService,
        CasesOptions options)
    {
        _context = context;
        _dateTimeService = dateTimeService;
        _options = options;
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
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityName = entityType,
            EntityId = entityId.ToString(),
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = _dateTimeService.UtcNow
        };

        await _context.AuditLogs.AddAsync(audit, cancellationToken);
    }

    public async Task AddOutboxAsync(
        string type,
        Guid entityId,
        object payload,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var envelope = new
        {
            payload,
            idempotencyKey
        };
        var serialized = JsonSerializer.Serialize(envelope, JsonOptions);

        if (!_pendingOutboxKeys.Add($"{type}:{idempotencyKey}"))
        {
            return;
        }

        var duplicate = await _context.OutboxMessages.AnyAsync(
            m => m.Type == type && m.Payload == serialized,
            cancellationToken);
        if (duplicate)
        {
            return;
        }

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = serialized,
            Status = OutboxMessageStatus.Pending,
            CreatedAt = _dateTimeService.UtcNow
        };

        await _context.OutboxMessages.AddAsync(message, cancellationToken);
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
