using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Entities;
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
}