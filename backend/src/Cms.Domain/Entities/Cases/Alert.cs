using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class Alert : BaseEntity
{
    public Guid? CrisisId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public AlertStatus Status { get; set; } = AlertStatus.Draft;
    public DateTime? PublishedAt { get; set; }
}
