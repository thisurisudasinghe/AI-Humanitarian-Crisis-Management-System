namespace Cms.Application.Cases.Interfaces;

/// <summary>
/// DEV2 abstraction over the shared M3 decision gate for Critical close.
/// DEV2 registers an adapter by default; M3 may replace it with the real governance
/// adapter during module composition.
/// </summary>
public interface ICloseCaseDecisionGate
{
    Task<bool> IsApprovedAsync(
        Guid decisionId,
        Guid requestId,
        CancellationToken cancellationToken = default);
}
