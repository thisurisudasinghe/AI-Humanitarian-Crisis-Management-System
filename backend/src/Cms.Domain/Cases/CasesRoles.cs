namespace Cms.Domain.Cases;

/// <summary>
/// Module-local copy of the frozen role strings. Contract request: replace with
/// Member 1's shared RoleNames constants when they are available.
/// </summary>
public static class CasesRoles
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

    public const string Staff = EmergencyCoordinator + "," + CaseManager + "," + SystemAdmin;
    public const string RequestReaders = Citizen + "," + Staff;
    public const string EmergencyCoordinators = EmergencyCoordinator + "," + SystemAdmin;
}
