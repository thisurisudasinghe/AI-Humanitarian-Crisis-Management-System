using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class PartnerShare : BaseEntity
{
    public Guid RequestId { get; set; }

    public string PartnerName { get; set; } = string.Empty;

    public string? PartnerOrganization { get; set; }

    public string? SharedData { get; set; }

    public DateTime SharedAt { get; set; } = DateTime.UtcNow;

    public string? SharedByUserId { get; set; }

    public string? Remarks { get; set; }

    public Request? Request { get; set; }
}
