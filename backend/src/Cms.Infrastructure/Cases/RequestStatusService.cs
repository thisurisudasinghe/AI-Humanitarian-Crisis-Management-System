using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class RequestStatusService :
    IRequestStatusService,
    IRequestMergeService,
    IRequestDecisionTransitionService
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly ICloseCaseDecisionGate _decisionGate;
    private readonly CaseWriteHelper _writer;

    public RequestStatusService(
        CmsDbContext context,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService,
        CaseWriteHelper writer,
        ICloseCaseDecisionGate decisionGate)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _writer = writer;
        _decisionGate = decisionGate;
    }

    public Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason = null,
        CancellationToken cancellationToken = default) =>
        TransitionCoreAsync(requestId, to, reason, decisionId: null, cancellationToken);

    public Task TransitionAsync(
        Guid requestId,
        RequestStatus to,
        string? reason,
        Guid? decisionId,
        CancellationToken cancellationToken = default) =>
        TransitionCoreAsync(requestId, to, reason, decisionId, cancellationToken);

    private async Task TransitionCoreAsync(
        Guid requestId,
        RequestStatus to,
        string? reason,
        Guid? decisionId,
        CancellationToken cancellationToken)
    {
        var request = await _context.Set<Request>()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");

        if (to == RequestStatus.Merged)
        {
            throw new ConflictException(
                "MERGE_TARGET_REQUIRED",
                "Use the merge endpoint to move a request to Merged.");
        }

        if (request.Status == to)
        {
            return;
        }

        // Validate the state-machine rule before applying the Critical-close gate so an
        // invalid transition is reported as 409 even when the request is Critical.
        RequestStateMachine.Ensure(request.Status, to);

        if (to == RequestStatus.Closed && request.Urgency == Urgency.Critical)
        {
            if (!decisionId.HasValue ||
                !await _decisionGate.IsApprovedAsync(decisionId.Value, request.Id, cancellationToken))
            {
                throw new ForbiddenException(
                    "CLOSE_DECISION_REQUIRED",
                    "Closing a Critical request requires an approved CloseCase decision.");
            }
        }

        var now = _dateTimeService.UtcNow;
        var from = request.Status;
        var version = await _writer.NextRequestVersionAsync(request.Id, cancellationToken);

        request.Status = to;
        request.UpdatedAt = now;
        switch (to)
        {
            case RequestStatus.Verified:
                request.VerifiedAt = now;
                break;
            case RequestStatus.Triaged:
                request.TriagedAt = now;
                break;
            case RequestStatus.Closed:
                request.ClosedAt = now;
                break;
        }

        await _writer.AddHistoryAsync(new RequestStatusHistory
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = _currentUser.UserId,
            Reason = reason,
            CreatedAt = now
        }, cancellationToken);

        await _writer.AddAuditAsync(
            "Request.StatusChanged",
            "Request",
            request.Id,
            new { from = from.ToString(), to = to.ToString(), reason },
            _currentUser.UserId,
            cancellationToken);

        await _writer.AddNotificationAsync(
            request.Id,
            request.RequesterUserId,
            $"request.status.{to}",
            new { from = from.ToString(), to = to.ToString(), reason },
            version,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MergeAsync(
        Guid sourceRequestId,
        Guid targetRequestId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceRequestId == targetRequestId)
        {
            throw new ConflictException("SELF_MERGE", "A request cannot be merged into itself.");
        }

        var source = await _context.Set<Request>()
            .FirstOrDefaultAsync(r => r.Id == sourceRequestId, cancellationToken)
            ?? throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {sourceRequestId} was not found.");

        var target = await _context.Set<Request>()
            .FirstOrDefaultAsync(r => r.Id == targetRequestId, cancellationToken)
            ?? throw new NotFoundException("MERGE_TARGET_NOT_FOUND", $"Request {targetRequestId} was not found.");

        if (RequestStateMachine.IsTerminal(target.Status))
        {
            throw new ConflictException(
                "TERMINAL_MERGE_TARGET",
                $"Request {targetRequestId} is in terminal status {target.Status}.");
        }

        RequestStateMachine.Ensure(source.Status, RequestStatus.Merged, hasMergeTarget: true);

        var now = _dateTimeService.UtcNow;
        var sourceVersion = await _writer.NextRequestVersionAsync(source.Id, cancellationToken);
        var targetVersion = await _writer.NextRequestVersionAsync(target.Id, cancellationToken);

        var sourceFrom = source.Status;
        source.Status = RequestStatus.Merged;
        source.MergedIntoRequestId = target.Id;
        source.UpdatedAt = now;

        await _writer.AddHistoryAsync(new RequestStatusHistory
        {
            Id = Guid.NewGuid(),
            RequestId = source.Id,
            FromStatus = sourceFrom,
            ToStatus = RequestStatus.Merged,
            ChangedByUserId = _currentUser.UserId,
            Reason = reason,
            CreatedAt = now
        }, cancellationToken);

        await _writer.AddHistoryAsync(new RequestStatusHistory
        {
            Id = Guid.NewGuid(),
            RequestId = target.Id,
            FromStatus = target.Status,
            ToStatus = target.Status,
            ChangedByUserId = _currentUser.UserId,
            Reason = $"Merge target for request {source.Id}. {reason}",
            CreatedAt = now
        }, cancellationToken);

        await _context.Set<RequestLink>().AddAsync(new RequestLink
        {
            Id = Guid.NewGuid(),
            RequestId = source.Id,
            LinkedRequestId = target.Id,
            LinkType = RequestLinkType.Merged,
            CreatedAt = now
        }, cancellationToken);

        await _writer.AddAuditAsync(
            "Request.Merged",
            "Request",
            source.Id,
            new { from = sourceFrom.ToString(), to = RequestStatus.Merged.ToString(), targetRequestId = target.Id, reason },
            _currentUser.UserId,
            cancellationToken);
        await _writer.AddAuditAsync(
            "Request.MergeTarget",
            "Request",
            target.Id,
            new { sourceRequestId = source.Id, reason },
            _currentUser.UserId,
            cancellationToken);

        await _writer.AddNotificationAsync(
            source.Id,
            source.RequesterUserId,
            "request.status.Merged",
            new { to = RequestStatus.Merged.ToString(), targetRequestId = target.Id },
            sourceVersion,
            cancellationToken);
        await _writer.AddNotificationAsync(
            target.Id,
            target.RequesterUserId,
            "request.status.Merged",
            new { sourceRequestId = source.Id },
            targetVersion,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
