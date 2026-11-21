using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.AI;

/// <summary>
/// Implements the required match orchestration. Matchers are decision-support
/// only: their result is stored as a recommendation and never performs an
/// allocation, shelter assignment or task assignment.
/// </summary>
public sealed class MatchRecommendationService : IMatchRecommendationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CmsDbContext _context;
    private readonly IResourceMatcher _resourceMatcher;
    private readonly IShelterRecommender _shelterMatcher;
    private readonly IVolunteerMatcher _volunteerMatcher;
    private readonly IRecommendationService _recommendations;
    private readonly IRequestStatusService _requestStatusService;
    private readonly IDateTimeService _dateTimeService;

    public MatchRecommendationService(
        CmsDbContext context,
        IResourceMatcher resourceMatcher,
        IShelterRecommender shelterMatcher,
        IVolunteerMatcher volunteerMatcher,
        IRecommendationService recommendations,
        IRequestStatusService requestStatusService,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _resourceMatcher = resourceMatcher;
        _shelterMatcher = shelterMatcher;
        _volunteerMatcher = volunteerMatcher;
        _recommendations = recommendations;
        _requestStatusService = requestStatusService;
        _dateTimeService = dateTimeService;
    }

    public async Task<MatchRecommendationResultDto> CreateAsync(
        MatchRecommendationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var type = request.Type?.Trim() ?? string.Empty;
        if (type is not (RecommendationTypes.ResourceMatch or
                         RecommendationTypes.ShelterMatch or
                         RecommendationTypes.VolunteerMatch))
        {
            throw new ValidationException(
                "Type must be ResourceMatch, ShelterMatch or VolunteerMatch.");
        }

        var target = await _context.Requests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken)
            ?? throw new NotFoundException("Target request was not found.");

        var ranked = await RankAsync(type, request.RequestId, cancellationToken);

        var candidates = ranked
            .OrderByDescending(candidate => candidate.Score)
            .Select(candidate => new MatchCandidateDto
            {
                TargetId = candidate.TargetId,
                TargetType = TargetTypeFor(type),
                Score = Math.Clamp(candidate.Score, 0d, 1d),
                Rationale = candidate.Rationale ?? string.Empty,
                IsApproximate = candidate.IsApproximate
            })
            .ToList();

        var payload = new
        {
            requestId = request.RequestId,
            type,
            top = candidates.FirstOrDefault(),
            alternatives = candidates.Skip(1).ToList(),
            generatedAt = _dateTimeService.UtcNow
        };

        var recommendationId = await _recommendations.CreateAsync(
            new CreateRecommendationRequest
            {
                Type = type,
                TargetId = request.RequestId,
                PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
                Confidence = candidates.Count == 0
                    ? 0m
                    : (decimal)Math.Clamp(candidates[0].Score, 0d, 1d),
                Rationale = candidates.Count == 0
                    ? "No ranked candidate was available. No critical action was taken."
                    : candidates[0].Rationale,
                Source = RecommendationSources.Rule,
                Status = RecommendationStatuses.Pending
            },
            cancellationToken);

        var advancesStatus =
            type is RecommendationTypes.ResourceMatch or RecommendationTypes.ShelterMatch &&
            candidates.Count > 0 &&
            target.Status == RequestStatus.Triaged;

        if (advancesStatus)
        {
            await _requestStatusService.TransitionAsync(
                target.Id,
                RequestStatus.Recommended,
                $"Stored human-review recommendation {recommendationId} ({type}).",
                cancellationToken);
            target.Status = RequestStatus.Recommended;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new MatchRecommendationResultDto
        {
            RecommendationId = recommendationId,
            Type = type,
            Status = RecommendationStatuses.Pending,
            RequestStatus = target.Status.ToString(),
            Candidates = candidates
        };
    }

    private async Task<IReadOnlyList<MatchCandidate>> RankAsync(
        string type,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return type switch
        {
            RecommendationTypes.ResourceMatch =>
                await _resourceMatcher.RankAsync(requestId, cancellationToken),
            RecommendationTypes.ShelterMatch =>
                await _shelterMatcher.RankAsync(requestId, cancellationToken),
            RecommendationTypes.VolunteerMatch =>
                await _volunteerMatcher.RankAsync(requestId, cancellationToken),
            _ => Array.Empty<MatchCandidate>()
        };
    }

    private static string TargetTypeFor(string type) => type switch
    {
        RecommendationTypes.ResourceMatch => "Resource",
        RecommendationTypes.ShelterMatch => "Shelter",
        RecommendationTypes.VolunteerMatch => "Volunteer",
        _ => "Request"
    };
}
