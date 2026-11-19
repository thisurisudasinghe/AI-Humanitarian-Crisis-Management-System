namespace Cms.Domain.Enums;

/// <summary>
/// Recorded outcome of a human decision on an AI recommendation (M4 Governance).
/// Persisted as a string by <c>ai.decision_records.outcome</c>, so the member
/// names are part of the stored contract.
/// </summary>
public enum DecisionOutcome
{
    Approve = 1,
    Modify = 2,
    Reject = 3,
    Defer = 4
}
