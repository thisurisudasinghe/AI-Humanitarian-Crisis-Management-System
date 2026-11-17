using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Area : BaseEntity
{
    public Guid CrisisId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? District { get; set; }

    public string? City { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public bool IsActive { get; set; } = true;
}
