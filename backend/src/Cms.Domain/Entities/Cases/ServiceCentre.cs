using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class ServiceCentre : BaseEntity
{
    public Guid? CrisisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    public string Services { get; set; } = string.Empty;
    public string? OpeningHours { get; set; }
    public bool IsActive { get; set; } = true;
}
