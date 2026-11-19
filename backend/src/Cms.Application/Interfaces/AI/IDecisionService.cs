
using Cms.Domain.Entities;
using Cms.Domain.Enums;

namespace Cms.Application.Interfaces.AI;

public interface IDecisionService
{
    Task<Guid> RecordDecisionAsync(
        Guid recommendationId,
        DecisionOutcome outcome,
        string reason,
        string? finalPayloadJson = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionRecord>> GetDecisionsAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default);
}
