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

    // Explicit public-map projection. These fields are deliberately separate
    // from the private Title/Body used by internal case work.
    public bool PublicVisible { get; set; }
    public string? PublicTitle { get; set; }
    public string? PublicSummary { get; set; }
    public string? PublicType { get; set; }
    public string? Severity { get; set; }
    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? PublicExpiresAt { get; set; }
    public Guid? PublishedByUserId { get; set; }
}
