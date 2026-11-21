using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Domain.Enums;
using Cms.Infrastructure.Cases;

namespace Cms.Tests.Cases;

public class DecisionGateCloseCaseAdapterTests
{
    private sealed class FakeDecisionGate : IDecisionGate
    {
        public Guid DecisionId { get; private set; }
        public RecommendationType ExpectedType { get; private set; }
        public Guid TargetId { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public bool Deny { get; init; }

        public Task<DecisionDto> RequireApprovedAsync(
            Guid decisionId,
            RecommendationType expectedType,
            Guid targetId,
            CancellationToken cancellationToken = default)
        {
            DecisionId = decisionId;
            ExpectedType = expectedType;
            TargetId = targetId;
            CancellationToken = cancellationToken;

            if (Deny)
            {
                throw new ForbiddenException("Decision denied.", "DECISION_DENIED");
            }

            return Task.FromResult(new DecisionDto(
                decisionId,
                DecisionOutcome.Approved,
                null,
                Guid.NewGuid(),
                DateTime.UtcNow));
        }
    }

    [Fact]
    public async Task Approved_decision_delegates_with_close_case_arguments()
    {
        var gate = new FakeDecisionGate();
        var adapter = new DecisionGateCloseCaseAdapter(gate);
        var decisionId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        using var source = new CancellationTokenSource();

        var approved = await adapter.IsApprovedAsync(decisionId, requestId, source.Token);

        Assert.True(approved);
        Assert.Equal(decisionId, gate.DecisionId);
        Assert.Equal(RecommendationType.CloseCase, gate.ExpectedType);
        Assert.Equal(requestId, gate.TargetId);
        Assert.Equal(source.Token, gate.CancellationToken);
    }

    [Fact]
    public async Task Denied_decision_returns_false()
    {
        var gate = new FakeDecisionGate { Deny = true };
        var adapter = new DecisionGateCloseCaseAdapter(gate);

        var approved = await adapter.IsApprovedAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        Assert.False(approved);
        Assert.Equal(RecommendationType.CloseCase, gate.ExpectedType);
    }
}
