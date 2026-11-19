using System.ComponentModel.DataAnnotations;
using Cms.Domain.Enums;

namespace Cms.Application.DTOs.Donations;

// ---------------------------------------------------------------------------
// Donors (FR-DON-01)
// ---------------------------------------------------------------------------

public class DonorDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DonorType DonorType { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsActive { get; set; }

    public int DonationCount { get; set; }

    public decimal TotalContributed { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class DonorQueryDto
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class CreateDonorRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DonorType DonorType { get; set; } = DonorType.Individual;

    [EmailAddress]
    [MaxLength(320)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    public bool IsAnonymous { get; set; }
}

public class UpdateDonorRequestDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public DonorType? DonorType { get; set; }

    [EmailAddress]
    [MaxLength(320)]
    public string? ContactEmail { get; set; }

    [MaxLength(20)]
    public string? ContactPhone { get; set; }

    public bool? IsAnonymous { get; set; }

    public bool? IsActive { get; set; }
}

// ---------------------------------------------------------------------------
// Campaigns (FR-DON-03, FR-DON-04)
// ---------------------------------------------------------------------------

public class CampaignDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal GoalAmount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public CampaignStatus Status { get; set; }

    public decimal RaisedAmount { get; set; }

    public decimal SpentAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public double ProgressPercent { get; set; }

    public int DonationCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CampaignQueryDto
{
    public string? Search { get; set; }

    public CampaignStatus? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class CreateCampaignRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal GoalAmount { get; set; }

    [MaxLength(3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a three-letter code.")]
    public string Currency { get; set; } = "USD";

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }
}

public class UpdateCampaignRequestDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? GoalAmount { get; set; }

    public DateTime? EndsAt { get; set; }

    public CampaignStatus? Status { get; set; }
}

public class CampaignExpenseDto
{
    public Guid Id { get; set; }

    public Guid CampaignId { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTime SpentAt { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateCampaignExpenseRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a three-letter code.")]
    public string? Currency { get; set; }

    public DateTime? SpentAt { get; set; }
}

// ---------------------------------------------------------------------------
// Donations (FR-DON-02, FR-DON-06)
// ---------------------------------------------------------------------------

public class DonationDto
{
    public Guid Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public Guid DonorId { get; set; }

    public string DonorName { get; set; } = string.Empty;

    public DonationType DonationType { get; set; }

    public decimal? Amount { get; set; }

    public string Currency { get; set; } = string.Empty;

    public string? ItemDescription { get; set; }

    public int? Quantity { get; set; }

    public decimal? EstimatedValue { get; set; }

    public decimal? AllocatableValue { get; set; }

    public decimal AllocatedAmount { get; set; }

    public decimal? UnallocatedAmount { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DonationStatus Status { get; set; }

    public bool IsSuspicious { get; set; }

    public string? SuspiciousReason { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<DonationAllocationDto> Allocations { get; set; }
        = Array.Empty<DonationAllocationDto>();
}

public class DonationQueryDto
{
    public Guid? DonorId { get; set; }

    public Guid? CampaignId { get; set; }

    public DonationType? DonationType { get; set; }

    public DonationStatus? Status { get; set; }

    public bool? IsSuspicious { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public class CreateDonationRequestDto
{
    public Guid DonorId { get; set; }

    public DonationType DonationType { get; set; } = DonationType.Monetary;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? Amount { get; set; }

    [MaxLength(3)]
    [RegularExpression("^[A-Za-z]{3}$", ErrorMessage = "Currency must be a three-letter code.")]
    public string? Currency { get; set; }

    [MaxLength(500)]
    public string? ItemDescription { get; set; }

    [Range(1, int.MaxValue)]
    public int? Quantity { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal? EstimatedValue { get; set; }

    public DateTime? ReceivedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class DonationAllocationDto
{
    public Guid Id { get; set; }

    public Guid DonationId { get; set; }

    public Guid CampaignId { get; set; }

    public string CampaignName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Note { get; set; }

    public bool IsSuspicious { get; set; }

    public string? SuspiciousReason { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class CreateDonationAllocationRequestDto
{
    public Guid CampaignId { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

/// <summary>Human review decision for a suspicious donation or allocation.</summary>
public class FlagRequestDto
{
    public bool IsSuspicious { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

// ---------------------------------------------------------------------------
// Transparency report (FR-DON-05)
// ---------------------------------------------------------------------------

public class DonationReportDto
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int TotalDonationCount { get; set; }

    public int TotalDonorCount { get; set; }

    public int SuspiciousDonationCount { get; set; }

    public int SuspiciousAllocationCount { get; set; }

    public IReadOnlyCollection<DonationCurrencyTotalDto> CurrencyTotals { get; set; }
        = Array.Empty<DonationCurrencyTotalDto>();

    public IReadOnlyCollection<CampaignReportLineDto> Campaigns { get; set; }
        = Array.Empty<CampaignReportLineDto>();
}

public class DonationCurrencyTotalDto
{
    public string Currency { get; set; } = string.Empty;

    public decimal MonetaryTotal { get; set; }

    public decimal MaterialEstimatedTotal { get; set; }

    public decimal AllocatedTotal { get; set; }

    public decimal SpentTotal { get; set; }

    public int DonationCount { get; set; }

    public int DonorCount { get; set; }

    public int SuspiciousCount { get; set; }
}

public class CampaignReportLineDto
{
    public Guid CampaignId { get; set; }

    public string Name { get; set; } = string.Empty;

    public CampaignStatus Status { get; set; }

    public string Currency { get; set; } = string.Empty;

    public decimal GoalAmount { get; set; }

    public decimal RaisedAmount { get; set; }

    public decimal SpentAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public double ProgressPercent { get; set; }
}
