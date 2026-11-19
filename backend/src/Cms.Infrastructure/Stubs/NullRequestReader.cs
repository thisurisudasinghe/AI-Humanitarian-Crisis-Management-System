using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using Cms.Domain.Enums.Cases;

namespace Cms.Infrastructure.Stubs;

/// <summary>Temporary request reader. Member 2 replaces this.</summary>
public sealed class NullRequestReader : IRequestReader
{
    public Task<RequestSummaryDto?> GetAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<RequestSummaryDto?>(null);
}

/// <summary>
/// Temporary status service. Member 2 replaces this. Throws instead of silently
/// pretending a transition happened.
/// </summary>
public sealed class NoOpRequestStatusService : IRequestStatusService
{
    public Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "The request status service is not implemented yet. Member 2 must replace this stub.");
}
