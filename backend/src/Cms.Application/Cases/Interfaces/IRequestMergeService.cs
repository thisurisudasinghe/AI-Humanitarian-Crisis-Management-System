namespace Cms.Application.Cases.Interfaces;

/// <summary>
/// Implemented by RequestStatusService so all request status writes remain in one class.
/// </summary>
public interface IRequestMergeService
{
    Task MergeAsync(
        Guid sourceRequestId,
        Guid targetRequestId,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
