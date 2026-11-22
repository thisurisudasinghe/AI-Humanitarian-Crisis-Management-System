using Cms.Domain.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;

namespace Cms.Tests.Cases;

public class RequestStateMachineTests
{
    private static readonly HashSet<(RequestStatus From, RequestStatus To)> Allowed = new()
    {
        (RequestStatus.Submitted, RequestStatus.Verified),
        (RequestStatus.Submitted, RequestStatus.Rejected),
        (RequestStatus.Verified, RequestStatus.Triaged),
        (RequestStatus.Verified, RequestStatus.Merged),
        (RequestStatus.Triaged, RequestStatus.Recommended),
        (RequestStatus.Triaged, RequestStatus.Merged),
        (RequestStatus.Recommended, RequestStatus.Approved),
        (RequestStatus.Approved, RequestStatus.Allocated),
        (RequestStatus.Allocated, RequestStatus.Dispatched),
        (RequestStatus.Allocated, RequestStatus.InProgress),
        (RequestStatus.Dispatched, RequestStatus.Fulfilled),
        (RequestStatus.InProgress, RequestStatus.Fulfilled),
        (RequestStatus.Fulfilled, RequestStatus.Closed),
        (RequestStatus.Closed, RequestStatus.Reopened),
        (RequestStatus.Submitted, RequestStatus.Escalated),
        (RequestStatus.Verified, RequestStatus.Escalated),
        (RequestStatus.Triaged, RequestStatus.Escalated),
        (RequestStatus.Recommended, RequestStatus.Escalated),
        (RequestStatus.Approved, RequestStatus.Escalated),
        (RequestStatus.Allocated, RequestStatus.Escalated),
        (RequestStatus.Dispatched, RequestStatus.Escalated),
        (RequestStatus.InProgress, RequestStatus.Escalated),
        (RequestStatus.Fulfilled, RequestStatus.Escalated),
        (RequestStatus.Submitted, RequestStatus.OnHold),
        (RequestStatus.Verified, RequestStatus.OnHold),
        (RequestStatus.Triaged, RequestStatus.OnHold),
        (RequestStatus.Recommended, RequestStatus.OnHold),
        (RequestStatus.Approved, RequestStatus.OnHold),
        (RequestStatus.Allocated, RequestStatus.OnHold),
        (RequestStatus.Dispatched, RequestStatus.OnHold),
        (RequestStatus.InProgress, RequestStatus.OnHold),
        (RequestStatus.Fulfilled, RequestStatus.OnHold),
        (RequestStatus.Escalated, RequestStatus.Escalated),
        (RequestStatus.Escalated, RequestStatus.OnHold),
        (RequestStatus.OnHold, RequestStatus.Escalated),
        (RequestStatus.OnHold, RequestStatus.OnHold),
        (RequestStatus.Reopened, RequestStatus.Escalated),
        (RequestStatus.Reopened, RequestStatus.OnHold)
    };

    public static IEnumerable<object[]> Matrix() =>
        from sourceStatus in Enum.GetValues<RequestStatus>()
        from targetStatus in Enum.GetValues<RequestStatus>()
        select new object[] { sourceStatus, targetStatus };

    [Theory]
    [MemberData(nameof(Matrix))]
    public void CanTransition_matches_frozen_matrix(RequestStatus from, RequestStatus to)
    {
        var expected = Allowed.Contains((from, to));
        Assert.Equal(expected, RequestStateMachine.CanTransition(from, to));

        if (expected)
        {
            RequestStateMachine.Ensure(from, to);
        }
        else
        {
            var ex = Assert.Throws<ConflictException>(() => RequestStateMachine.Ensure(from, to));
            Assert.Equal("INVALID_TRANSITION", ex.Code);
            Assert.Contains(from.ToString(), ex.Message);
            Assert.Contains(to.ToString(), ex.Message);
        }
    }

    [Fact]
    public void Open_states_are_exactly_the_non_terminal_states()
    {
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            Assert.Equal(!RequestStateMachine.IsTerminal(status), RequestStateMachine.IsOpen(status));
        }
    }

    [Fact]
    public void Closed_is_reachable_only_from_Fulfilled()
    {
        var sources = Allowed.Where(pair => pair.To == RequestStatus.Closed).Select(pair => pair.From).ToList();
        Assert.Equal(new[] { RequestStatus.Fulfilled }, sources);
    }

    [Fact]
    public void Merged_without_target_is_rejected()
    {
        var ex = Assert.Throws<ConflictException>(
            () => RequestStateMachine.Ensure(RequestStatus.Verified, RequestStatus.Merged, hasMergeTarget: false));
        Assert.Equal("INVALID_TRANSITION", ex.Code);
        Assert.Contains("target request", ex.Message);
    }

    [Fact]
    public void Merged_with_target_is_allowed_from_Verified_and_Triaged()
    {
        RequestStateMachine.Ensure(RequestStatus.Verified, RequestStatus.Merged, hasMergeTarget: true);
        RequestStateMachine.Ensure(RequestStatus.Triaged, RequestStatus.Merged, hasMergeTarget: true);
    }
}
