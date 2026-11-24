using Cms.Domain.Common;

namespace Cms.Domain.Entities;

/// <summary>
/// A structured item inside a material donation. Keeps custom descriptions,
/// categories and units as recorded by the donor instead of flattening them
/// into free-text notes.
/// </summary>
public class DonationItem : BaseEntity
{
    public Guid DonationId { get; set; }

    public Donation Donation { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public int Quantity { get; set; }

    public string? Unit { get; set; }

    public string? Condition { get; set; }

    public string? Notes { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Packaging { get; set; }
}
