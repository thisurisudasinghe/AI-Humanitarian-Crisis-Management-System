using Cms.Application.Contracts;

namespace Cms.Infrastructure.Stubs;

/// <summary>Discards audit events. Only for tests and tooling, never in production.</summary>
public sealed class NoOpAuditLogger : IAuditLogger
{
    public Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
