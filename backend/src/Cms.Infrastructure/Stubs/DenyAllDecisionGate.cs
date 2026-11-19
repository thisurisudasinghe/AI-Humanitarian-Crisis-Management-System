using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Domain.Enums;

namespace Cms.Infrastructure.Stubs;

/// <summary>
/// Fails closed. No critical action may proceed until Member 3 registers the real gate.
/// </summary>
public sealed class DenyAllDecisionGate : IDecisionGate
{
    public Task<DecisionDto> RequireApprovedAsync(
        Guid decisionId,
        RecommendationType expectedType,
        Guid targetId,
        CancellationToken cancellationToken = default) =>
        throw new ForbiddenException(
            "No approved decision is available: the decision gate is not implemented yet.",
            "DECISION_GATE_UNAVAILABLE");
}
