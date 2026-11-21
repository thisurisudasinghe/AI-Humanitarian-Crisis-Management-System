using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class RequestLink : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid LinkedRequestId { get; set; }
    public RequestLinkType LinkType { get; set; }
}
