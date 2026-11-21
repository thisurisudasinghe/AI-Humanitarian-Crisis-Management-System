namespace Cms.Domain.Constants;

/// <summary>
/// The ten system roles frozen by the shared contract (Block S).
/// Never use raw role strings anywhere else in the codebase.
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
        SystemAdmin
    ];
}
