using Cms.Application.Interfaces.AI;
using Cms.Application.Interfaces.Governance;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using GovernanceDecisionDto = Cms.Application.DTOs.Governance.DecisionDto;
using SharedDecisionDto = Cms.Application.Contracts.DecisionDto;
using SharedForbiddenException = Cms.Application.Common.ForbiddenException;
using GovernanceForbiddenException = Cms.Application.Exceptions.ForbiddenException;

namespace Cms.Infrastructure.Services.Governance;

/// <summary>
/// Validates that a critical action is backed by a recorded, still-applicable
/// human decision. It never performs the action itself. Replaces both the
/// foundation fail-closed gate and the Cases module placeholder.
/// </summary>
public sealed class DecisionGate :
    IDecisionGate,
    Cms.Application.Contracts.IDecisionGate
{
    private readonly CmsDbContext _context;
    private readonly IRecommendationService _recommendationService;

    public DecisionGate(
        CmsDbContext context,
        IRecommendationService recommendationService)
    {
        _context = context;
        _recommendationService = recommendationService;
    }

    public async Task<GovernanceDecisionDto> RequireApprovedAsync(
        Guid decisionId,
        string expectedRecommendationType,
        Guid targetRequestId,
        CancellationToken cancellationToken = default)
    {
        var decision = await GetApprovedDecisionAsync(decisionId, cancellationToken);
        var recommendation = await GetApplicableRecommendationAsync(
            decision.RecommendationId,
            expectedRecommendationType,
            targetRequestId,
            cancellationToken);

        _ = recommendation;

        return new GovernanceDecisionDto
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

    public async Task<SharedDecisionDto> RequireApprovedAsync(
        Guid decisionId,
        RecommendationType expectedType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var decision = await GetApprovedDecisionAsync(decisionId, cancellationToken);
            await GetApplicableRecommendationAsync(
                decision.RecommendationId,
                expectedType.ToString(),
                targetId,
                cancellationToken);

            return new SharedDecisionDto(
                decision.Id,
                decision.Outcome,
                decision.FinalPayloadJson,
                decision.DecidedByUserId,
                decision.DecidedAt);
        }
        catch (GovernanceForbiddenException ex)
        {
            // The shared contract's exception is the one mapped by the API middleware.
            throw new SharedForbiddenException(ex.Message);
        }
    }

    private async Task<DecisionRecord> GetApprovedDecisionAsync(
        Guid decisionId,
        CancellationToken cancellationToken)
    {
        var decision = await _context.DecisionRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == decisionId, cancellationToken);

        if (decision == null)
        {
            throw new GovernanceForbiddenException(
                "The referenced decision does not exist. A recorded human decision is required.");
        }

        if (decision.Outcome is not (DecisionOutcome.Approved or DecisionOutcome.Modified))
        {
            throw new GovernanceForbiddenException(
                "The referenced decision is not an approval. Rejected and deferred decisions " +
                "cannot authorise a critical action.");
        }

        return decision;
    }

    private async Task<Recommendation> GetApplicableRecommendationAsync(
        Guid recommendationId,
        string expectedType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var recommendation = await _recommendationService.GetByIdAsync(
            recommendationId,
            cancellationToken);

        if (recommendation == null)
        {
            throw new GovernanceForbiddenException(
                "The recommendation linked to the decision no longer exists.");
        }

        if (!string.Equals(
                recommendation.Type,
                expectedType,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new GovernanceForbiddenException(
                $"The decision applies to recommendation type '{recommendation.Type}', " +
                $"but '{expectedType}' is required for this action.");
        }

        if (recommendation.TargetId != targetId)
        {
            throw new GovernanceForbiddenException(
                "The decision applies to a different request than the one being acted upon.");
        }

        return recommendation;
    }
}
