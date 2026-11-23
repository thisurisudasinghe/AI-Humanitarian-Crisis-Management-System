namespace Cms.Domain.Constants;

/// <summary>
/// System roles used by the shared contract. Public self-registration may only
/// assign safe public-participation roles through server-side policy.
/// </summary>
public static class RoleNames
{
    public const string Citizen = "Citizen";
    public const string EmergencyCoordinator = "EmergencyCoordinator";
    public const string CaseManager = "CaseManager";
    public const string LogisticsManager = "LogisticsManager";
    public const string ShelterAdmin = "ShelterAdmin";
    public const string VolunteerCoordinator = "VolunteerCoordinator";
    public const string Volunteer = "Volunteer";
    public const string DonorOfficer = "DonorOfficer";
    public const string PartnerOfficer = "PartnerOfficer";
    public const string SystemAdmin = "SystemAdmin";
    public const string Donor = "Donor";

    public static readonly IReadOnlyList<string> All =
    [
        Citizen,
        EmergencyCoordinator,
        CaseManager,
        LogisticsManager,
        ShelterAdmin,
        VolunteerCoordinator,
        Volunteer,
        DonorOfficer,
        PartnerOfficer,
        SystemAdmin,
        Donor
    ];

    /// <summary>
    /// Roles that may be granted through the public self-registration flow.
    /// Privileged/staff roles must never be assigned by public registration.
    /// </summary>
    public static readonly IReadOnlyList<string> PublicSelfRegistrationRoles =
    [
        Citizen,
        Volunteer,
        Donor
    ];
}
