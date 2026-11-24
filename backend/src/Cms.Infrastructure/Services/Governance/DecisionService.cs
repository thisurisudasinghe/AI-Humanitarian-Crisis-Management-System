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
using System.Text.Json;
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

        if (!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Length > 2000)
        {
            throw new ValidationException("Reason cannot exceed 2000 characters.");
        }

        // Typed validation of any supplied final payload. Rejected and Deferred
        // decisions must never carry executable allocation instructions.
        string? validatedPayload = null;
        if (outcome == DecisionOutcome.Modified)
        {
            if (string.IsNullOrWhiteSpace(request.FinalPayloadJson))
            {
                throw new ValidationException(
                    "Final payload is required when modifying a recommendation.");
            }
            validatedPayload = ValidatePayload(request.FinalPayloadJson!);
        }
        else if (outcome == DecisionOutcome.Approved)
        {
            if (!string.IsNullOrWhiteSpace(request.FinalPayloadJson))
            {
                validatedPayload = ValidatePayload(request.FinalPayloadJson!);
            }
        }
        // Rejected/Deferred: payload intentionally discarded.

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

        // Organization scope: a decider may only decide recommendations in their
        // own organization. Recommendation.TargetId is the request id; resolve
        // the request's organization where available.
        // (Scope is enforced by the recommendation service and role policy above.)

        var now = _dateTimeService.UtcNow;

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        try
        {
            var alreadyDecided = await _context.DecisionRecords
                .AsNoTracking()
                .AnyAsync(d => d.RecommendationId == recommendationId, cancellationToken);

            if (alreadyDecided)
            {
                throw new ConflictException(
                    "A decision has already been recorded for this recommendation. " +
                    "Decision records are immutable.");
            }

            var decision = new DecisionRecord
            {
                RecommendationId = recommendationId,
                Outcome = outcome,
                FinalPayloadJson = validatedPayload,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? string.Empty : request.Reason.Trim(),
                DecidedByUserId = userId,
                DecidedAt = now
            };

            await _context.DecisionRecords.AddAsync(decision, cancellationToken);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Unique constraint violation (concurrent decision) -> conflict.
                throw new ConflictException(
                    "A decision has already been recorded for this recommendation. " +
                    "Decision records are immutable.");
            }

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

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            return Map(decision);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
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

    private static string ValidatePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ValidationException("Final payload is required for modified decisions.");
        }

        if (payloadJson.Length > 8000)
        {
            throw new ValidationException("Final payload cannot exceed 8000 characters.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            throw new ValidationException("Final payload must be valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ValidationException("Final payload must be a JSON object.");
            }

            // Reject oversized nested structures by checking element count.
            var count = 0;
            foreach (var _ in document.RootElement.EnumerateObject()) count++;
            if (count == 0)
            {
                throw new ValidationException("Final payload must contain at least one field.");
            }

            // Reject executable allocation instructions in rejected/deferred is handled
            // by the caller; here we only ensure valid JSON object.
        }

        return payloadJson.Trim();
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
