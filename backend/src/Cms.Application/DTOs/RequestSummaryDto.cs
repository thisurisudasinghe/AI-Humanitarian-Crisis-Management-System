using Cms.Domain.Enums.Cases;

namespace Cms.Application.DTOs;

public class RequestSummaryDto
{
    public Guid Id { get; set; }

    public Category Category { get; set; }

    public Urgency Urgency { get; set; }

    public RequestStatus Status { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal? Lat { get; set; }

    public decimal? Lng { get; set; }

    public Guid AreaId { get; set; }

    public int HouseholdSize { get; set; }

    public string? AccessibilityNeeds { get; set; }

    public DateTime CreatedAt { get; set; }
}
