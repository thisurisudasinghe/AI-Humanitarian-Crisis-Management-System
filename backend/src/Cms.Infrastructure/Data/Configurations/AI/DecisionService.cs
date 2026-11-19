
using System.Text.Json;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.AI;

public class DecisionService : IDecisionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IDateTimeService _dateTimeService;

    public DecisionService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _dateTimeService = dateTimeService;
    }

    public async Task<Guid> RecordDecisionAsync(
        Guid recommendationId,
        DecisionOutcome outcome,
        string reason,
        string? finalPayloadJson = null,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue)
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

        var recommendationExists = await _context.Recommendations
            .AnyAsync(x => x.Id == recommendationId, cancellationToken);

        if (!recommendationExists)
            throw new KeyNotFoundException("Recommendation was not found.");

        var decision = new DecisionRecord
        {
            RecommendationId = recommendationId,
            Outcome = outcome,
            FinalPayloadJson = outcome == DecisionOutcome.Modified
                ? finalPayloadJson
                : null,
            Reason = reason.Trim(),
            DecidedByUserId = _currentUserService.UserId.Value,
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

        await _auditService.LogAsync(
            _currentUserService.UserId,
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
