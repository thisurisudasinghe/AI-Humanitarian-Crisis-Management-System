using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>A person or organisation that gives donations (M8).</summary>
public class Donor : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public DonorType DonorType { get; set; } = DonorType.Individual;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>Anonymous donors are aggregated in the transparency report.</summary>
    public bool IsAnonymous { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Linked public user account, when this donor enrolled through the portal.</summary>
    public Guid? UserId { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
}
