using Cms.Application.DTOs.Common;
using Cms.Application.DTOs.Governance;
using Cms.Application.Interfaces.Governance;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Governance;

/// <summary>
/// Read-only projection over recommendations and their human decisions.
/// It deliberately exposes both the AI output and what a human actually did with it.
/// </summary>
public sealed class PostIncidentReviewService : IPostIncidentReviewService
{
    private readonly ApplicationDbContext _context;

    public PostIncidentReviewService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ReviewEntryDto>> GetDecisionsAsync(
        Guid? crisisId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from recommendation in _context.Recommendations.AsNoTracking()
            join request in _context.Requests.AsNoTracking()
                on recommendation.TargetId equals request.Id into requests
            from request in requests.DefaultIfEmpty()
            join decision in _context.DecisionRecords.AsNoTracking()
                on recommendation.Id equals decision.RecommendationId into decisions
            from decision in decisions.DefaultIfEmpty()
            where crisisId == null || (request != null && request.CrisisId == crisisId.Value)
            orderby recommendation.CreatedAt descending
            select new ReviewEntryDto
            {
                RecommendationId = recommendation.Id,
                Type = recommendation.Type,
                TargetId = recommendation.TargetId,
                Source = recommendation.Source,
                Confidence = recommendation.Confidence,
                Rationale = recommendation.Rationale,
                PayloadJson = recommendation.PayloadJson,
                DecisionId = decision == null ? null : decision.Id,
                Outcome = decision == null ? null : decision.Outcome.ToString(),
                Reason = decision == null ? null : decision.Reason,
                FinalPayloadJson = decision == null ? null : decision.FinalPayloadJson,
                DecidedBy = decision == null ? null : decision.DecidedByUserId,
                DecidedAt = decision == null ? null : decision.DecidedAt,
                IsOverride = decision != null &&
                    (decision.Outcome == DecisionOutcome.Modified ||
                     decision.Outcome == DecisionOutcome.Rejected)
            };

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewEntryDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }
}
