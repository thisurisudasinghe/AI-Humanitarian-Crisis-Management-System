using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

public sealed record DecisionDto(
    Guid Id,
    DecisionOutcome Outcome,
    string? FinalPayloadJson,
    Guid DecidedBy,
    DateTime DecidedAt);
