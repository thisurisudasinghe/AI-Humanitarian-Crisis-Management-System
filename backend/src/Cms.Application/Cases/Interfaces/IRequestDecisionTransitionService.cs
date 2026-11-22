using Cms.Domain.Enums.Cases;

namespace Cms.Application.Cases.Interfaces;

/// <summary>
/// Module-local extension of the frozen IRequestStatusService contract.
/// It carries a validated CloseCase decision id into the single status-write service.
/// </summary>
public interface IRequestDecisionTransitionService
{
    Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason = null,
        Guid? decisionId = null,
        CancellationToken cancellationToken = default);
}
