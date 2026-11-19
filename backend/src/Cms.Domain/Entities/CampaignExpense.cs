using Cms.Domain.Common;

namespace Cms.Domain.Entities;

/// <summary>Money spent against a campaign (FR-DON-04).</summary>
public class CampaignExpense : BaseEntity
{
    public Guid CampaignId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public DateTime SpentAt { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public Campaign Campaign { get; set; } = null!;
}
