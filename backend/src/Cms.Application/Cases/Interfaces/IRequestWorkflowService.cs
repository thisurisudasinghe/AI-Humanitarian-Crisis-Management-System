using Cms.Application.Cases.DTOs;

namespace Cms.Application.Cases.Interfaces;

public interface IRequestWorkflowService
{
    Task TransitionAsync(
        Guid requestId,
        TransitionRequestDto request,
        CancellationToken cancellationToken = default);
}
