using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

/// <summary>
/// The ONLY way to change a request status. Implemented by Member 2 (M2).
/// Every transition writes history + audit and enqueues Notification.Send.
/// </summary>
public interface IRequestStatusService
{
    Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
