using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>A fundraising campaign that donations are allocated to (M8).</summary>
public class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal GoalAmount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public Guid? CreatedByUserId { get; set; }

    public ICollection<DonationAllocation> Allocations { get; set; }
        = new List<DonationAllocation>();

    public ICollection<CampaignExpense> Expenses { get; set; }
        = new List<CampaignExpense>();
}
