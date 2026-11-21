using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class PartnerShare : BaseEntity
{
    public Guid RequestId { get; set; }
    public Guid OrganisationId { get; set; }
    public Guid SharedByUserId { get; set; }
    public DateTime SharedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
