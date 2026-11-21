using Cms.Domain.Common;

namespace Cms.Domain.Entities;

/// <summary>
/// Splits the value of a donation across campaigns (FR-DON-03). Every
/// allocation is audited and constrained so the total never exceeds the
/// allocatable value of the donation.
/// </summary>
public class DonationAllocation : BaseEntity
{
    public Guid DonationId { get; set; }

    public Guid CampaignId { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    /// <summary>Set when the allocation trips a review rule (for example over-goal).</summary>
    public bool IsSuspicious { get; set; }

    public string? SuspiciousReason { get; set; }

    public Guid? AllocatedByUserId { get; set; }

    public Donation Donation { get; set; } = null!;

    public Campaign Campaign { get; set; } = null!;
}
