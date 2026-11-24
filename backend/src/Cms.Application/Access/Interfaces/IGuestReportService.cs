using Cms.Application.Access.DTOs;

namespace Cms.Application.Access.Interfaces;

public interface IGuestReportService
{
    Task<GuestReportReceiptDto> SubmitAsync(
        CreateGuestReportDto request,
        CancellationToken cancellationToken = default);

    Task<GuestReportTrackingDto> TrackAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Internal operational queue; caller authorization is enforced by the API.</summary>
    Task<IReadOnlyList<GuestReportSummaryDto>> ListForOperationsAsync(
        CancellationToken cancellationToken = default);

    Task<GuestReportTrackingDto> UpdateStatusAsync(
        Guid reportId,
        UpdateGuestReportStatusDto request,
        CancellationToken cancellationToken = default);
}
