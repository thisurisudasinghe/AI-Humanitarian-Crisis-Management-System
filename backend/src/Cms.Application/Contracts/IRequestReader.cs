namespace Cms.Application.Contracts;

/// <summary>Read-only view of a request. Implemented by Member 2 (M2).</summary>
public interface IRequestReader
{
    Task<RequestSummaryDto?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);
}
