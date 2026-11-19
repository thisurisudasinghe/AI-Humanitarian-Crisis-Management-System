namespace Cms.Application.Cases.Interfaces;

/// <summary>
/// Module-local placeholder for Member 3's frozen IDecisionGate contract.
/// Until that contract exists, the default implementation denies CloseCase.
/// </summary>
public interface ICloseCaseDecisionGate
{
    Task<bool> IsApprovedAsync(
        Guid decisionId,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
