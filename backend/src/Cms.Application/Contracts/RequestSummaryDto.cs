using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

public sealed record RequestSummaryDto(
    Guid Id,
    Category Category,
    Urgency Urgency,
    RequestStatus Status,
    string Description,
    double Lat,
    double Lng,
    Guid? AreaId,
    int HouseholdSize,
    string? AccessibilityNeeds,
    DateTime CreatedAt);
