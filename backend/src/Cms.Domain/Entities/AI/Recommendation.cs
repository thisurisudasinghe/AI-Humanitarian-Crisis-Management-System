using Cms.Domain.Common;

namespace Cms.Domain.Entities;

public class Recommendation : BaseEntity
{
    public string Type { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string Rationale { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}