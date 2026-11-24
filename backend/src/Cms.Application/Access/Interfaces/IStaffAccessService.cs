using Cms.Application.Access.DTOs;

namespace Cms.Application.Access.Interfaces;

public interface IStaffAccessService
{
    Task<StaffApplicationReceiptDto> SubmitAsync(
        SubmitStaffApplicationDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffApplicationDto>> ListMineAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Reviewer queue; caller authorization is enforced by the API.</summary>
    Task<IReadOnlyList<StaffApplicationDto>> ListForReviewAsync(
        CancellationToken cancellationToken = default);

    Task<StaffApplicationDto> DecideAsync(
        Guid applicationId,
        ReviewStaffApplicationDto request,
        CancellationToken cancellationToken = default);
}
