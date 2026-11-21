using Cms.Application.DTOs.Announcements;
using Cms.Application.DTOs.Common;

namespace Cms.Application.Interfaces.Announcements;

public interface IAnnouncementService
{
    /// <summary>Creates an announcement in PendingApproval. No delivery occurs here.</summary>
    Task<AnnouncementDto> CreateAsync(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AnnouncementDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves the announcement and fans out idempotent Notification.Send outbox
    /// messages to the selected audience. Requires a role-appropriate approver.
    /// </summary>
    Task<AnnouncementDto> ApproveAsync(
        Guid id,
        ApproveAnnouncementRequest request,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDto> RejectAsync(
        Guid id,
        RejectAnnouncementRequest request,
        CancellationToken cancellationToken = default);
}
