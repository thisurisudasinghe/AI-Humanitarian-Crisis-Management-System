using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Governance;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Governance;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Governance;

public sealed class DecisionService : IDecisionService
{
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationService _recommendationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IDateTimeService _dateTimeService;

    public DecisionService(
        ApplicationDbContext context,
        IRecommendationService recommendationService,
        ICurrentUserService currentUser,
        IAuditService auditService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _recommendationService = recommendationService;
        _currentUser = currentUser;
        _auditService = auditService;
        _dateTimeService = dateTimeService;
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

        var recommendation = await _recommendationService.GetByIdAsync(
            recommendationId,
            cancellationToken);

        if (recommendation == null)
        {
            throw new NotFoundException("Recommendation was not found.");
        }

        if (!DecisionRolePolicy.CanDecide(
                recommendation.Type,
                _currentUser.Roles))
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

        await _auditService.LogAsync(
            userId,
            "Decision.Recorded",
            "DecisionRecord",
            decision.Id.ToString(),
            $"Recommendation={recommendationId};Outcome={outcome};Type={recommendation.Type}",
            cancellationToken);

        return new DecisionDto
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
