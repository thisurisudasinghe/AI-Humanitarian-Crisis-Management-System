using Cms.Application.Cases.Interfaces;
using Cms.Domain.Enums;

namespace Cms.Infrastructure.Services.Governance;

/// <summary>
/// Adapts the shared decision gate to the Cases module's local close-case contract.
/// Returns true only when the shared gate accepts the recorded human decision.
/// </summary>
public sealed class CloseCaseDecisionGateAdapter : ICloseCaseDecisionGate
{
    private readonly Cms.Application.Contracts.IDecisionGate _gate;

    public CloseCaseDecisionGateAdapter(Cms.Application.Contracts.IDecisionGate gate)
    {
        _gate = gate;
    }

    public async Task<bool> IsApprovedAsync(
        Guid decisionId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.RequireApprovedAsync(
                decisionId,
                RecommendationType.CloseCase,
                requestId,
                cancellationToken);
            return true;
        }
        catch (Cms.Application.Common.ForbiddenException)
        {
            return false;
        }
    }
}
