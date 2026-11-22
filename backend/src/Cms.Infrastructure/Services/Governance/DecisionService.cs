using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Governance;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Governance;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CurrentUserContract = Cms.Application.Contracts.ICurrentUser;
using IAuditLogger = Cms.Application.Contracts.IAuditLogger;

namespace Cms.Infrastructure.Services.Governance;

/// <summary>
/// Records immutable human decisions on AI recommendations and exposes the
/// read model used by the decision gate and post-incident review.
/// </summary>
public sealed class DecisionService : Cms.Application.Interfaces.Governance.IDecisionService
{
    private readonly CmsDbContext _context;
    private readonly IRecommendationService _recommendations;
    private readonly CurrentUserContract _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeService _dateTimeService;
    private readonly IRequestStatusService _requestStatusService;

    public DecisionService(
        CmsDbContext context,
        IRecommendationService recommendations,
        CurrentUserContract currentUser,
        IAuditLogger auditLogger,
        IDateTimeService dateTimeService,
        IRequestStatusService requestStatusService)
    {
        _context = context;
        _recommendations = recommendations;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _dateTimeService = dateTimeService;
        _requestStatusService = requestStatusService;
    }

    public async Task<DecisionDto> RecordAsync(
        Guid recommendationId,
        DecisionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<DecisionOutcome>(request.Outcome, true, out var outcome) ||
            !Enum.IsDefined(outcome))
        {
            throw new ValidationException(
                "Outcome must be one of: Approved, Modified, Rejected, Deferred.");
        }

        if ((outcome is DecisionOutcome.Rejected or DecisionOutcome.Deferred or DecisionOutcome.Modified) &&
            string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(
                "A reason is required for Modified, Rejected and Deferred decisions.");
        }

        var recommendation = await _recommendations.GetByIdAsync(
            recommendationId,
            cancellationToken);

        if (recommendation == null)
        {
            throw new NotFoundException("Recommendation was not found.");
        }

        if (!DecisionRolePolicy.CanDecide(recommendation.Type, _currentUser.Roles))
        {
            throw new ForbiddenException(
                $"Your role is not authorised to decide on '{recommendation.Type}' recommendations.");
        }

        if (_currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("An authenticated user is required to record a decision.");
        }

        var alreadyDecided = await _context.DecisionRecords
            .AsNoTracking()
            .AnyAsync(d => d.RecommendationId == recommendationId, cancellationToken);

        if (alreadyDecided)
        {
            throw new ConflictException(
                "A decision has already been recorded for this recommendation. " +
                "Decision records are immutable.");
        }

        var now = _dateTimeService.UtcNow;
        var decision = new DecisionRecord
        {
            RecommendationId = recommendationId,
            Outcome = outcome,
            FinalPayloadJson = string.IsNullOrWhiteSpace(request.FinalPayloadJson)
                ? null
                : request.FinalPayloadJson,
            Reason = request.Reason.Trim(),
            DecidedByUserId = userId,
            DecidedAt = now
        };

        await _context.DecisionRecords.AddAsync(decision, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Recommendations are append-only; the decision record carries the outcome.

        var advancesRequest =
            outcome is DecisionOutcome.Approved or DecisionOutcome.Modified &&
            recommendation.Type is RecommendationTypes.ResourceMatch or RecommendationTypes.ShelterMatch;

        if (advancesRequest)
        {
            var targetRequest = await _context.Requests
                .FirstOrDefaultAsync(r => r.Id == recommendation.TargetId, cancellationToken);

            if (targetRequest?.Status == RequestStatus.Recommended)
            {
                await _requestStatusService.TransitionAsync(
                    targetRequest.Id,
                    RequestStatus.Approved,
                    $"Human decision {decision.Id} ({outcome}) approved the {recommendation.Type} recommendation.",
                    cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            "Decision.Recorded",
            "DecisionRecord",
            decision.Id.ToString(),
            new
            {
                Recommendation = recommendationId,
                Confidence = recommendation.Confidence,
                Outcome = outcome.ToString(),
                Type = recommendation.Type
            },
            cancellationToken);

        return Map(decision);
    }

    public async Task<DecisionDto?> GetByIdAsync(
        Guid decisionId,
        CancellationToken cancellationToken = default)
    {
        var decision = await _context.DecisionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        return decision == null ? null : Map(decision);
    }

    public async Task<IReadOnlyList<DecisionDto>> GetByRecommendationAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        var decisions = await _context.DecisionRecords
            .AsNoTracking()
            .Where(d => d.RecommendationId == recommendationId)
            .OrderBy(d => d.DecidedAt)
            .ToListAsync(cancellationToken);

        return decisions.Select(Map).ToList();
    }

    private static DecisionDto Map(DecisionRecord decision) => new()
    {
        Id = decision.Id,
        RecommendationId = decision.RecommendationId,
        Outcome = decision.Outcome,
        FinalPayloadJson = decision.FinalPayloadJson,
        Reason = decision.Reason,
        DecidedBy = decision.DecidedByUserId,
        DecidedAt = decision.DecidedAt
    };
}
