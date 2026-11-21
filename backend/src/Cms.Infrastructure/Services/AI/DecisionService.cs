
using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.Governance;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.AI;

public class DecisionService : IDecisionService
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly IDateTimeService _dateTimeService;

    public DecisionService(
        CmsDbContext context,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> RecordDecisionAsync(
        Guid recommendationId,
        DecisionOutcome outcome,
        string reason,
        string? finalPayloadJson = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated ||
            !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "An authenticated user is required.");
        }

        if (!Enum.IsDefined(typeof(DecisionOutcome), outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A decision reason is required.");

        if (reason.Length > 2000)
            throw new ArgumentException(
                "Decision reason cannot exceed 2000 characters.");

        if (outcome == DecisionOutcome.Modified &&
            string.IsNullOrWhiteSpace(finalPayloadJson))
        {
            throw new ArgumentException(
                "Final payload is required when modifying a recommendation.");
        }

        var recommendation = await _context.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == recommendationId, cancellationToken);

        if (recommendation == null)
            throw new KeyNotFoundException("Recommendation was not found.");

        if (!DecisionRolePolicy.CanDecide(recommendation.Type, _currentUser.Roles))
        {
            throw new UnauthorizedAccessException(
                $"Your role is not authorised to decide on '{recommendation.Type}' recommendations.");
        }

        var alreadyDecided = await _context.DecisionRecords
            .AsNoTracking()
            .AnyAsync(x => x.RecommendationId == recommendationId, cancellationToken);

        if (alreadyDecided)
        {
            throw new InvalidOperationException(
                "A decision has already been recorded for this recommendation. " +
                "Decision records are immutable.");
        }

        var decision = new DecisionRecord
        {
            RecommendationId = recommendationId,
            Outcome = outcome,
            FinalPayloadJson = outcome == DecisionOutcome.Modified
                ? finalPayloadJson
                : null,
            Reason = reason.Trim(),
            DecidedByUserId = _currentUser.UserId.Value,
            DecidedAt = _dateTimeService.UtcNow
        };

        await _context.DecisionRecords.AddAsync(
            decision, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var auditDetails = JsonSerializer.Serialize(new
        {
            RecommendationId = recommendationId,
            Outcome = outcome.ToString(),
            Reason = reason.Trim(),
            FinalPayloadJson = decision.FinalPayloadJson
        });

        await _auditLogger.LogAsync(
            _currentUser.UserId,
            $"Recommendation.{outcome}",
            nameof(Recommendation),
            recommendationId.ToString(),
            auditDetails,
            cancellationToken);

        return decision.Id;
    }

    public async Task<IReadOnlyList<DecisionRecord>> GetDecisionsAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        var recommendationExists = await _context.Recommendations
            .AnyAsync(x => x.Id == recommendationId, cancellationToken);

        if (!recommendationExists)
            throw new KeyNotFoundException("Recommendation was not found.");

        return await _context.DecisionRecords
            .Where(x => x.RecommendationId == recommendationId)
            .OrderByDescending(x => x.DecidedAt)
            .ToListAsync(cancellationToken);
    }
}
