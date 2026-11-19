using System.Text.Json;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.AI;

public class RecommendationService : IRecommendationService
{
    private readonly CmsDbContext _context;

    public RecommendationService(CmsDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> CreateAsync(
        CreateRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Confidence < 0 || request.Confidence > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Confidence),
                "Confidence must be between 0 and 1.");
        }

        var recommendation = new Recommendation
        {
            Type = request.Type,
            TargetId = request.TargetId,
            PayloadJson = request.PayloadJson,
            Confidence = request.Confidence,
            Rationale = request.Rationale,
            Source = request.Source
        };

        await _context.Recommendations.AddAsync(
            recommendation, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return recommendation.Id;
    }

    public async Task<Recommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Recommendations
            .FirstOrDefaultAsync(
                x => x.Id == recommendationId,
                cancellationToken);
    }

    public async Task<Guid> GenerateForCrisisAsync(
        Guid crisisId,
        CancellationToken cancellationToken = default)
    {
        var crisis = await _context.Set<Crisis>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == crisisId,
                cancellationToken);

        if (crisis is null)
        {
            throw new KeyNotFoundException(
                $"Crisis with ID {crisisId} was not found.");
        }

        // D2-01/D2-02 own the canonical Crisis/Area shape: areas have no
        // IsActive flag, and crisis severity/type are carried by Crisis.Type.
        var activeAreas = await _context.Set<Area>()
            .AsNoTracking()
            .Where(a => a.CrisisId == crisisId)
            .OrderBy(a => a.Name)
            .Select(a => a.Name)
            .ToListAsync(cancellationToken);

        var suggestedActions = crisis.Type.ToLowerInvariant() switch
        {
            "critical" or "high" => new[]
            {
                "Prioritize emergency response.",
                "Assess evacuation requirements.",
                "Coordinate urgent assistance for affected areas."
            },

            "medium" => new[]
            {
                "Prepare emergency response resources.",
                "Monitor affected areas.",
                "Coordinate assistance where required."
            },

            _ => new[]
            {
                "Monitor the crisis situation.",
                "Review local preparedness.",
                "Update response plans when necessary."
            }
        };

        var payload = new
        {
            CrisisId = crisis.Id,
            CrisisName = crisis.Name,
            CrisisType = crisis.Type,
            Severity = crisis.Type,
            ActiveAreas = activeAreas,
            SuggestedActions = suggestedActions,
            HumanReviewRequired = true
        };

        var recommendation = new Recommendation
        {
            Type = "CrisisResponse",
            TargetId = crisis.Id,
            PayloadJson = JsonSerializer.Serialize(payload),
            Confidence = 0.70m,
            Rationale = "Rule-based recommendation generated using crisis severity and active area information. Human review is required.",
            Source = "RuleBased"
        };

        await _context.Recommendations.AddAsync(
            recommendation, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return recommendation.Id;
    }

}