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

    /// <summary>Optional campaign the donor wants to support. Allocation remains staff-controlled.</summary>
    public Guid? CampaignId { get; set; }

    public Campaign? Campaign { get; set; }

    /// <summary>Normalised supply category for a material donation, when supplied.</summary>
    public string? Category { get; set; }

    /// <summary>Normalised unit for a single-item material donation, when supplied.</summary>
    public string? Unit { get; set; }

    /// <summary>Human-readable drop-off or donation location, never raw coordinates alone.</summary>
    public string? LocationText { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Supported collection or drop-off method, when the donor supplied one.</summary>
    public string? DeliveryMethod { get; set; }

    /// <summary>Client-generated key used to replay a submit without creating a duplicate donation.</summary>
    public string? IdempotencyKey { get; set; }

    public ICollection<DonationItem> Items { get; set; } = new List<DonationItem>();

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
