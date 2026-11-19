using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

/// <summary>
/// Human decision gate. Implemented by Member 3 (M4 Governance).
/// Must throw ForbiddenException unless the decision is Approved/Modified and matches type + target.
/// </summary>
public interface IDecisionGate
{
    Task<DecisionDto> RequireApprovedAsync(
        Guid decisionId,
        RecommendationType expectedType,
        Guid targetId,
        CancellationToken cancellationToken = default);
}
