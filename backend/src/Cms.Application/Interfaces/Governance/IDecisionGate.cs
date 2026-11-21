using Cms.Application.DTOs.Governance;

namespace Cms.Application.Interfaces.Governance;

/// <summary>
/// Guards critical actions. A critical action may only proceed when a recorded
/// human decision exists and is still applicable to the recommendation/target.
/// Implementations must throw ForbiddenException when the gate is not satisfied.
/// </summary>
public interface IDecisionGate
{
    Task<DecisionDto> RequireApprovedAsync(
        Guid decisionId,
        string expectedRecommendationType,
        Guid targetRequestId,
        CancellationToken cancellationToken = default);
}
