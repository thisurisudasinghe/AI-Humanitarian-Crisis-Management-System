using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;

namespace Cms.Domain.Cases;

/// <summary>
/// Pure request status transition table. No I/O and no domain services.
/// </summary>
public static class RequestStateMachine
{
    private static readonly IReadOnlyDictionary<RequestStatus, IReadOnlySet<RequestStatus>> Transitions =
        new Dictionary<RequestStatus, IReadOnlySet<RequestStatus>>
        {
            [RequestStatus.Submitted] = Only(RequestStatus.Verified, RequestStatus.Rejected),
            [RequestStatus.Verified] = Only(RequestStatus.Triaged, RequestStatus.Merged),
            [RequestStatus.Triaged] = Only(RequestStatus.Recommended, RequestStatus.Merged),
            [RequestStatus.Recommended] = Only(RequestStatus.Approved),
            [RequestStatus.Approved] = Only(RequestStatus.Allocated),
            [RequestStatus.Allocated] = Only(RequestStatus.Dispatched, RequestStatus.InProgress),
            [RequestStatus.Dispatched] = Only(RequestStatus.Fulfilled),
            [RequestStatus.InProgress] = Only(RequestStatus.Fulfilled),
            [RequestStatus.Fulfilled] = Only(RequestStatus.Closed),
            [RequestStatus.Closed] = Only(RequestStatus.Reopened),
            [RequestStatus.Reopened] = Only(
                RequestStatus.Triaged, RequestStatus.Escalated, RequestStatus.OnHold),
            [RequestStatus.Escalated] = Only(
                RequestStatus.Triaged, RequestStatus.Escalated, RequestStatus.OnHold),
            [RequestStatus.OnHold] = Only(
                RequestStatus.Triaged, RequestStatus.Escalated, RequestStatus.OnHold),
            [RequestStatus.Rejected] = Only(),
            [RequestStatus.Merged] = Only()
        };

    // "Any open state" means every state that is not terminal. Escalated,
    // OnHold and Reopened remain live cases, so they keep the frozen side-path
    // transitions to Escalated or OnHold. NFR-REL-05: the explicit entries above
    // add the conservative recovery path back to Triaged for a partially
    // completed workflow; no other forward transition is unlocked from them.
    private static readonly IReadOnlySet<RequestStatus> OpenStates =
        Enum.GetValues<RequestStatus>().Where(status => !IsTerminal(status)).ToHashSet();

    public static bool CanTransition(RequestStatus from, RequestStatus to)
    {
        if (OpenStates.Contains(from) &&
            (to == RequestStatus.Escalated || to == RequestStatus.OnHold))
        {
            return true;
        }

        return Transitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static void Ensure(RequestStatus from, RequestStatus to) =>
        Ensure(from, to, hasMergeTarget: true);

    public static void Ensure(RequestStatus from, RequestStatus to, bool hasMergeTarget)
    {
        if (!CanTransition(from, to))
        {
            throw new ConflictException(
                "INVALID_TRANSITION",
                $"Cannot change request status from {from} to {to}.");
        }

        if (to == RequestStatus.Merged && !hasMergeTarget)
        {
            throw new ConflictException(
                "INVALID_TRANSITION",
                $"Cannot change request status from {from} to {to} without a target request.");
        }
    }

    public static bool IsTerminal(RequestStatus status) =>
        status is RequestStatus.Rejected or RequestStatus.Closed or RequestStatus.Merged;

    public static bool IsOpen(RequestStatus status) => OpenStates.Contains(status);

    private static IReadOnlySet<RequestStatus> Only(params RequestStatus[] statuses) =>
        new HashSet<RequestStatus>(statuses);
}
