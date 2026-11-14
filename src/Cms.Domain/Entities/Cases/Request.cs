using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class Request : BaseEntity
{
    public Guid HouseholdId { get; set; }

    public Guid CrisisId { get; set; }

    public string RequestTitle { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string AssistanceType { get; set; } = string.Empty;

    public string UrgencyLevel { get; set; } = string.Empty;

    public RequestStatus Status { get; set; } = RequestStatus.Submitted;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public string? AssignedCoordinatorId { get; set; }

    public string? Remarks { get; set; }

    public bool IsDeleted { get; set; } = false;

    // Navigation Properties
    public Household? Household { get; set; }

    public Crisis? Crisis { get; set; }

    public ICollection<RequestStatusHistory> StatusHistory { get; set; }
        = new List<RequestStatusHistory>();
}
