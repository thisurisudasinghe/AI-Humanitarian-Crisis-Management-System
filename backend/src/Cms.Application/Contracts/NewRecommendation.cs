using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

public sealed record NewRecommendation(
    RecommendationType Type,
    Guid TargetId,
    string PayloadJson,
    double Confidence,
    string Rationale,
    RecommendationSource Source);
