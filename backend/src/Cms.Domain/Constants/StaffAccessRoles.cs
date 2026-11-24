namespace Cms.Domain.Constants;

/// <summary>
/// Roles that may be requested through the public staff-access application.
/// SystemAdmin is intentionally excluded: administrative trust is granted only
/// through the existing admin-controlled user management workflow.
/// </summary>
public static class StaffAccessRoles
{
    public static readonly IReadOnlyList<string> Requestable =
    [
        RoleNames.EmergencyCoordinator,
        RoleNames.CaseManager,
        RoleNames.LogisticsManager,
        RoleNames.ShelterAdmin,
        RoleNames.VolunteerCoordinator,
        RoleNames.DonorOfficer,
        RoleNames.PartnerOfficer
    ];

    public static bool IsRequestable(string? role) =>
        !string.IsNullOrWhiteSpace(role) &&
        Requestable.Contains(role.Trim(), StringComparer.Ordinal);
}
