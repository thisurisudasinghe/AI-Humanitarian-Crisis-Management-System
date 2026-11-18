using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class RequestStatusHistory : BaseEntity
{
    public Guid RequestId { get; set; }
    public RequestStatus FromStatus { get; set; }
    public RequestStatus ToStatus { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? Reason { get; set; }
}
