using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Append-only audit trail. Writes one <see cref="AuditEvent"/> row and commits it
/// immediately, so call it after the business change has been committed.
/// Details are serialized to JSON and masked before storage.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDateTimeService _dateTimeService;

    public AuditLogger(
        CmsDbContext context,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _dateTimeService = dateTimeService;
    }

    public Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(_currentUser.UserId, action, entityType, entityId, details, cancellationToken);
    }

    public Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(actorUserId, action, entityType, entityId, details, cancellationToken);
    }

    private async Task WriteAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId,
        object? details,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("An audit action is required.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            throw new ArgumentException("An audit entity type is required.", nameof(entityType));
        }

        var auditEvent = new AuditEvent
        {
            Action = action.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId,
            ActorUserId = actorUserId,
            Details = SensitiveDataMasker.MaskObject(details),
            IpAddress = ResolveIpAddress(),
            CreatedAt = _dateTimeService.UtcNow
        };

        await _context.AuditEvents.AddAsync(auditEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private string? ResolveIpAddress()
    {
        var connection = _httpContextAccessor.HttpContext?.Connection;
        return connection?.RemoteIpAddress?.ToString();
    }
}
