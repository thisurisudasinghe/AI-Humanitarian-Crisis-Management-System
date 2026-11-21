using System.Text.Json;
using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Persists decision-support recommendations. It never performs a critical action.
/// Supports both the v1 recommendation API and the crisis-generation endpoint.
/// </summary>
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
        ArgumentNullException.ThrowIfNull(request);

        if (!RecommendationTypes.All.Contains(request.Type, StringComparer.Ordinal))
        {
            throw new ValidationException($"Invalid recommendation type: {request.Type}");
        }

        if (!RecommendationSources.All.Contains(request.Source, StringComparer.Ordinal))
        {
            throw new ValidationException($"Invalid recommendation source: {request.Source}");
        }

        if (request.TargetId == Guid.Empty)
        {
            throw new ValidationException("TargetId is required.");
        }

        if (request.Confidence < 0 || request.Confidence > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Confidence),
                "Confidence must be between 0 and 1.");
        }

        // Repeated outbox delivery must not create duplicate recommendations.
        var existing = await _context.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TargetId == request.TargetId &&
                     r.Type == request.Type &&
                     r.PayloadJson == request.PayloadJson &&
                     r.Source == request.Source,
                cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        var recommendation = new Recommendation
        {
            Type = request.Type,
            TargetId = request.TargetId,
            PayloadJson = request.PayloadJson,
            Confidence = request.Confidence,
            Rationale = request.Rationale,
            Source = request.Source,
            Status = string.IsNullOrWhiteSpace(request.Status)
                ? RecommendationStatuses.Pending
                : request.Status
        };

        await _context.Recommendations.AddAsync(recommendation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return recommendation.Id;
    }

    public Task<Recommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        return _context.Recommendations
            .FirstOrDefaultAsync(x => x.Id == recommendationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Recommendation>> GetByTargetAsync(
        Guid targetId,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTypeFilter(type);

        return await _context.Recommendations
            .AsNoTracking()
            .Where(r => r.TargetId == targetId &&
                        (type == null || r.Type == type))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recommendation>> GetAllAsync(
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        ValidateTypeFilter(type);

        return await _context.Recommendations
            .AsNoTracking()
            .Where(r => type == null || r.Type == type)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatchCandidateDto>> GetMatchesAsync(
        MatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.Equals(request.TargetType, "Request", StringComparison.OrdinalIgnoreCase))
        {
            // Resource/Shelter/Volunteer matchers are owned by other developers and
            // are not yet available in this branch. No incompatible stub is invented.
            return Array.Empty<MatchCandidateDto>();
        }

        var target = await _context.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.TargetId, cancellationToken);

        if (target == null)
        {
            throw new NotFoundException("Target request was not found.");
        }

        var targetTokens = Tokenize(
            $"{target.Category} {target.Urgency} {target.Description}");

        var candidates = await _context.Requests
            .AsNoTracking()
            .Where(r => r.Id != target.Id && r.Status != RequestStatus.Closed)
            .Take(100)
            .ToListAsync(cancellationToken);

        var results = new List<MatchCandidateDto>();

        foreach (var candidate in candidates)
        {
            var tokens = Tokenize(
                $"{candidate.Category} {candidate.Urgency} {candidate.Description}");
            var union = targetTokens.Union(tokens).Count();
            var score = union == 0 ? 0 : (double)targetTokens.Intersect(tokens).Count() / union;

            if (score < 0.1)
            {
                continue;
            }

            results.Add(new MatchCandidateDto
            {
                TargetId = candidate.Id,
                TargetType = "Request",
                Score = Math.Round(score, 3),
                Rationale = "Similar non-identifying keywords; candidate only, no automatic action.",
                IsApproximate = true
            });
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(10)
            .ToList();
    }

    public async Task<Guid> GenerateForCrisisAsync(
        Guid crisisId,
        CancellationToken cancellationToken = default)
    {
        var crisis = await _context.Set<Crisis>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == crisisId, cancellationToken);

        if (crisis is null)
        {
            throw new NotFoundException($"Crisis with ID {crisisId} was not found.");
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
            Type = RecommendationTypes.Summary,
            TargetId = crisis.Id,
            PayloadJson = JsonSerializer.Serialize(payload),
            Confidence = 0.70m,
            Rationale = "Rule-based recommendation generated using crisis severity and active area information. Human review is required.",
            Source = RecommendationSources.Rule
        };

        // Reuse an identical generated recommendation when the event is redelivered.
        var existing = await _context.Recommendations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.TargetId == crisis.Id &&
                     r.Type == recommendation.Type &&
                     r.PayloadJson == recommendation.PayloadJson &&
                     r.Source == recommendation.Source,
                cancellationToken);

        if (existing != null)
        {
            return existing.Id;
        }

        await _context.Recommendations.AddAsync(recommendation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return recommendation.Id;
    }

    private static void ValidateTypeFilter(string? type)
    {
        if (type != null && !RecommendationTypes.All.Contains(type, StringComparer.Ordinal))
        {
            throw new ValidationException($"Invalid recommendation type: {type}");
        }
    }

    private static HashSet<string> Tokenize(string text)
    {
        return text.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 2)
            .Select(token => new string(token.Where(char.IsLetter).ToArray()))
            .Where(token => token.Length > 2)
            .ToHashSet();
    }
}
