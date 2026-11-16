
namespace Cms.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId = null,
        string? details = null,
        CancellationToken cancellationToken = default);
}
