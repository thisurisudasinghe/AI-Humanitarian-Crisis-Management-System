using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;

namespace Cms.Application.Cases.Interfaces;

public interface IRequestService
{
    Task<Guid> SubmitAsync(CreateRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResult<RequestListItemDto>> ListMineAsync(
        PagedQuery query,
        CancellationToken cancellationToken = default);
    Task<RequestDetailDto?> GetDetailAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task UpdateMineAsync(Guid requestId, UpdateRequestDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestTimelineEntryDto>> GetTimelineAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
    Task<Guid> AddFeedbackAsync(
        Guid requestId,
        CreateFeedbackDto feedback,
        CancellationToken cancellationToken = default);
    Task<PagedResult<RequestListItemDto>> ListStaffAsync(
        RequestFilter filter,
        PagedQuery query,
        CancellationToken cancellationToken = default);
    Task AssignAsync(Guid requestId, AssignRequestDto request, CancellationToken cancellationToken = default);
}
