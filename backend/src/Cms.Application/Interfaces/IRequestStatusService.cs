using Cms.Domain.Enums.Cases;

namespace Cms.Application.Interfaces;

public interface IRequestStatusService
{
    Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason = null,
        CancellationToken cancellationToken = default);
}