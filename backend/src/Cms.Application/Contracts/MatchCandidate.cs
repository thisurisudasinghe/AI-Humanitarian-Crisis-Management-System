namespace Cms.Application.Contracts;

public sealed record MatchCandidate(
    Guid TargetId,
    double Score,
    string Rationale,
    bool IsApproximate);
