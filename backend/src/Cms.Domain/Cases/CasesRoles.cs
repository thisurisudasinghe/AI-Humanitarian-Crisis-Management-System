using Cms.Domain.Constants;

namespace Cms.Domain.Cases;

/// <summary>
/// Module-local aliases for the shared frozen role names. The contract request
/// is resolved by referencing Member 1's RoleNames constants instead of raw strings.
/// </summary>
public static class CasesRoles
{
    public const string Citizen = RoleNames.Citizen;
    public const string EmergencyCoordinator = RoleNames.EmergencyCoordinator;
    public const string CaseManager = RoleNames.CaseManager;
    public const string LogisticsManager = RoleNames.LogisticsManager;
    public const string ShelterAdmin = RoleNames.ShelterAdmin;
    public const string VolunteerCoordinator = RoleNames.VolunteerCoordinator;
    public const string Volunteer = RoleNames.Volunteer;
    public const string DonorOfficer = RoleNames.DonorOfficer;
    public const string PartnerOfficer = RoleNames.PartnerOfficer;
    public const string SystemAdmin = RoleNames.SystemAdmin;

    // D2-07/D2-08 restrict case workflow and crisis writes to these exact roles.
    public const string CaseStaff = EmergencyCoordinator + "," + CaseManager;
    public const string CoordinatorOnly = EmergencyCoordinator;

    public const string Staff = CaseStaff;
    public const string RequestReaders = Citizen + "," + Staff;
}
