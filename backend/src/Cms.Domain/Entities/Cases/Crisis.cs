using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Crisis : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string CrisisType { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Property
    public ICollection<Area> Areas { get; set; } = new List<Area>();
}