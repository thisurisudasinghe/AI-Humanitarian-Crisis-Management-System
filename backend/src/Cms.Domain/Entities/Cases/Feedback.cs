using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Feedback : BaseEntity
{
    public Guid RequestId { get; set; }

    public string? UserId { get; set; }

    public int Rating { get; set; }

    public string? Comments { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Request? Request { get; set; }
}
