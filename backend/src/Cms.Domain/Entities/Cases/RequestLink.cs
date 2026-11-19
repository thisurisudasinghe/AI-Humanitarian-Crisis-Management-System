using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class RequestLink : BaseEntity
{
    public Guid SourceRequestId { get; set; }

    public Guid TargetRequestId { get; set; }

    public string LinkType { get; set; } = string.Empty;

    public string? Remarks { get; set; }

    public string? LinkedByUserId { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public Request? SourceRequest { get; set; }

    public Request? TargetRequest { get; set; }
}
