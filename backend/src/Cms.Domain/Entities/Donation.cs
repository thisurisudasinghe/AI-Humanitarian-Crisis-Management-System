using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>
/// A recorded monetary or material donation (M8). Monetary donations use
/// <see cref="Amount"/>; material donations use <see cref="ItemDescription"/>
/// and optionally <see cref="Quantity"/> and <see cref="EstimatedValue"/>.
/// </summary>
public class Donation : BaseEntity
{
    /// <summary>Human-readable reference, unique across all donations.</summary>
    public string Reference { get; set; } = string.Empty;

    public Guid DonorId { get; set; }

    public DonationType DonationType { get; set; } = DonationType.Monetary;

    public decimal? Amount { get; set; }

    public string Currency { get; set; } = "USD";

    public string? ItemDescription { get; set; }

    public int? Quantity { get; set; }

    public decimal? EstimatedValue { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DonationStatus Status { get; set; } = DonationStatus.Received;

    /// <summary>Rule-based flag; a human reviewer clears it via the flag endpoint.</summary>
    public bool IsSuspicious { get; set; }

    public string? SuspiciousReason { get; set; }

    public string? Notes { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public Donor Donor { get; set; } = null!;

    public ICollection<DonationAllocation> Allocations { get; set; }
        = new List<DonationAllocation>();
}
