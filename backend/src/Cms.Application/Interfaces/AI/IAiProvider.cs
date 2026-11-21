using Cms.Application.DTOs.AI;
using Cms.Domain.Entities.Cases;

namespace Cms.Application.Interfaces.AI;

public interface IAiProvider
{
    string Name { get; }

    /// <summary>
    /// Produces recommendations only. Never performs or triggers a critical action.
    /// </summary>
    Task<AiAnalysisResult> AnalyzeAsync(
        Request request,
        IReadOnlyList<Request> candidateRequests,
        CancellationToken cancellationToken = default);
}
