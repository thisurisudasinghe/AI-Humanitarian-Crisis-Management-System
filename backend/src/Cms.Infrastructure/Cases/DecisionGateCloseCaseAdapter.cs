using Cms.Application.Cases.Interfaces;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Domain.Enums;

namespace Cms.Infrastructure.Cases;

/// <summary>
/// DEV2-owned composition adapter. It delegates the Critical close decision to
/// the shared M3 <see cref="IDecisionGate"/> contract using the frozen
/// <see cref="RecommendationType.CloseCase"/> type and the request as target.
/// The adapter contains no decision lookup or approval logic; Member 3 owns the
/// real implementation and registration of <see cref="IDecisionGate"/>.
/// </summary>
public sealed class DecisionGateCloseCaseAdapter : ICloseCaseDecisionGate
{
    private readonly IDecisionGate _decisionGate;

    public DecisionGateCloseCaseAdapter(IDecisionGate decisionGate)
    {
        _decisionGate = decisionGate;
    }

    public async Task<bool> IsApprovedAsync(
        Guid decisionId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _decisionGate.RequireApprovedAsync(
                decisionId,
                RecommendationType.CloseCase,
                requestId,
                cancellationToken);
            return true;
        }
        catch (ForbiddenException)
        {
            // The shared gate denies an unapproved or mismatched decision.
            // RequestStatusService translates false into CLOSE_DECISION_REQUIRED.
            return false;
        }
    }
}
