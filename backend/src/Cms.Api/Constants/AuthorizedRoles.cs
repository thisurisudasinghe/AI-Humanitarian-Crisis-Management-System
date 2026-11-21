using Cms.Domain.Constants;

namespace Cms.Api.Constants;

/// <summary>
/// Compile-time role groups for controller-level RBAC. The governance service
/// still applies the exact per-recommendation decider matrix.
/// </summary>
public static class AuthorizedRoles
{
    public const string All =
        Roles.Citizen + "," + Roles.EmergencyCoordinator + "," + Roles.CaseManager + "," +
        Roles.LogisticsManager + "," + Roles.ShelterAdmin + "," + Roles.VolunteerCoordinator + "," +
        Roles.Volunteer + "," + Roles.DonorOfficer + "," + Roles.PartnerOfficer + "," +
        Roles.SystemAdmin;

    public const string Staff =
        Roles.EmergencyCoordinator + "," + Roles.CaseManager + "," + Roles.LogisticsManager + "," +
        Roles.ShelterAdmin + "," + Roles.VolunteerCoordinator + "," + Roles.DonorOfficer + "," +
        Roles.PartnerOfficer + "," + Roles.SystemAdmin;

    /// <summary>Union of the frozen per-type decision roles; SystemAdmin is excluded.</summary>
    public const string Deciders =
        Roles.CaseManager + "," + Roles.EmergencyCoordinator + "," + Roles.LogisticsManager + "," +
        Roles.ShelterAdmin + "," + Roles.VolunteerCoordinator;
}
