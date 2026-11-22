using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class Request : BaseEntity
{
    public Guid RequesterUserId { get; set; }
    public Guid? CrisisId { get; set; }
    public Guid? AreaId { get; set; }
    public Category Category { get; set; }
    public Urgency Urgency { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public string? Address { get; set; }
    public int HouseholdSize { get; set; }
    public string? AccessibilityNeeds { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Submitted;
    public Guid? AssignedToUserId { get; set; }
    public Guid? MergedIntoRequestId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? TriagedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
