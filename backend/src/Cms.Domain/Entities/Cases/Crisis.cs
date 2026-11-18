using Cms.Domain.Common;
using Cms.Domain.Enums.Cases;

namespace Cms.Domain.Entities.Cases;

public class Crisis : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CrisisStatus Status { get; set; } = CrisisStatus.Active;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
