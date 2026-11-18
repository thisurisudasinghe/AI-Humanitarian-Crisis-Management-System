using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
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
            Source = request.Source
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

        var targetTokens = Tokenize($"{target.Title} {target.Description}");

        var candidates = await _context.Requests
            .AsNoTracking()
            .Where(r => r.Id != target.Id && !r.IsDeleted)
            .Take(100)
            .ToListAsync(cancellationToken);

        var results = new List<MatchCandidateDto>();

        foreach (var candidate in candidates)
        {
            var tokens = Tokenize($"{candidate.Title} {candidate.Description}");
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
