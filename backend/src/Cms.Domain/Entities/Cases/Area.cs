using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Area : BaseEntity
{
    public Guid CrisisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public decimal? RadiusKm { get; set; }
}
