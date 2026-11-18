using Cms.Domain.Common;

namespace Cms.Domain.Entities.Cases;

public class Household : BaseEntity
{
    public Guid AreaId { get; set; }

    public string HouseholdName { get; set; } = string.Empty;

    public string HeadOfHouseholdName { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public string? Address { get; set; }

    public int HouseholdSize { get; set; }

    public int? NumberOfChildren { get; set; }

    public int? NumberOfElderly { get; set; }

    public int? NumberOfDisabledMembers { get; set; }

    public string? VulnerabilityLevel { get; set; }

    public bool IsVerified { get; set; } = false;

    // Navigation Property
    public Area? Area { get; set; }
}