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
    private readonly ApplicationDbContext _context;

    public RecommendationService(ApplicationDbContext context)
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
            .Include(c => c.Areas)
            .FirstOrDefaultAsync(
                c => c.Id == crisisId,
                cancellationToken);

        if (crisis is null)
        {
            throw new KeyNotFoundException(
                $"Crisis with ID {crisisId} was not found.");
        }

        var activeAreas = crisis.Areas
            .Where(a => a.IsActive)
            .Select(a => a.Name)
            .ToList();

        var suggestedActions = crisis.Severity.ToLowerInvariant() switch
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
            CrisisType = crisis.CrisisType,
            Severity = crisis.Severity,
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