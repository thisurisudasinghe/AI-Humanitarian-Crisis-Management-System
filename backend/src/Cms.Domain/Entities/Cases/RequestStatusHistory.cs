using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class RequestStatusHistory : BaseEntity
{
    public Guid RequestId { get; set; }

    public RequestStatus PreviousStatus { get; set; }

    public RequestStatus NewStatus { get; set; }

    public string? ChangedByUserId { get; set; }

    public string? Remarks { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Navigation Property
    public Request? Request { get; set; }
}
