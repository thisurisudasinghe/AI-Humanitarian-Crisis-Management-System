using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Feedback : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid SubmittedByUserId { get; set; }
    public string Comment { get; set; } = string.Empty;
}
