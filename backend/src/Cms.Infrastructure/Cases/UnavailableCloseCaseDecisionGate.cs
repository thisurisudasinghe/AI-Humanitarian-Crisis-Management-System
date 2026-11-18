using Cms.Application.Cases.Interfaces;

namespace Cms.Infrastructure.Cases;

/// <summary>
/// Fails closed until Member 3 ships IDecisionGate and RecommendationType.CloseCase.
/// Contract request: replace this registration with the real gate.
/// </summary>
public sealed class UnavailableCloseCaseDecisionGate : ICloseCaseDecisionGate
{
    public Task<bool> IsApprovedAsync(
        Guid decisionId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
