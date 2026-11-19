namespace Cms.Application.Contracts;

/// <summary>
/// Append-only audit trail. Implemented by Member 1 (M1).
/// Details are serialized to JSON and masked before storage.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>Additive overload for system tasks with no HTTP principal.</summary>
    Task LogAsync(
        Guid? actorUserId,
        string action,
        string entityType,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default);
}
