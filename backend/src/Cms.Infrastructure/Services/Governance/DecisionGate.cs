using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Governance;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Governance;

/// <summary>
/// Validates that a critical action is backed by a recorded, still-applicable
/// human decision. It never performs the action itself.
/// </summary>
public sealed class DecisionGate : IDecisionGate
{
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationService _recommendationService;

    public DecisionGate(
        ApplicationDbContext context,
        IRecommendationService recommendationService)
    {
        _context = context;
        _recommendationService = recommendationService;
    }

    public async Task<DecisionDto> RequireApprovedAsync(
        Guid decisionId,
        string expectedRecommendationType,
        Guid targetRequestId,
        CancellationToken cancellationToken = default)
    {
        var decision = await _context.DecisionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision == null)
        {
            throw new ForbiddenException(
                "The referenced decision does not exist. A recorded human decision is required.");
        }

        if (decision.Outcome is not (DecisionOutcome.Approved or DecisionOutcome.Modified))
        {
            throw new ForbiddenException(
                "The referenced decision is not an approval. Rejected and deferred decisions " +
                "cannot authorise a critical action.");
        }

        var recommendation = await _recommendationService.GetByIdAsync(
            decision.RecommendationId,
            cancellationToken);

        if (recommendation == null)
        {
            throw new ForbiddenException(
                "The recommendation linked to the decision no longer exists.");
        }

        if (!string.Equals(
                recommendation.Type,
                expectedRecommendationType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException(
                $"The decision applies to recommendation type '{recommendation.Type}', " +
                $"but '{expectedRecommendationType}' is required for this action.");
        }

        if (recommendation.TargetId != targetRequestId)
        {
            throw new ForbiddenException(
                "The decision applies to a different request than the one being acted upon.");
        }

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
}
