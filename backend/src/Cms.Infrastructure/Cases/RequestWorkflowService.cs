using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;

namespace Cms.Infrastructure.Cases;

public sealed class RequestWorkflowService : IRequestWorkflowService
{
    private readonly IRequestDecisionTransitionService _statusService;

    public RequestWorkflowService(IRequestDecisionTransitionService statusService)
    {
        _statusService = statusService;
    }

    public Task TransitionAsync(
        Guid requestId,
        TransitionRequestDto request,
        CancellationToken cancellationToken = default) =>
        _statusService.TransitionAsync(
            requestId,
            request.To,
            request.Reason,
            request.DecisionId,
            cancellationToken);
}
