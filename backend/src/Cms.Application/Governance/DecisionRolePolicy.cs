using Cms.Domain.Constants;

namespace Cms.Application.Governance;

/// <summary>
/// Role-appropriate approval policy. Critical decisions require a role that owns
/// the affected domain; SystemAdmin can always act as a break-glass approver.
/// </summary>
public static class DecisionRolePolicy
{
    public static IReadOnlyCollection<string> RolesFor(string recommendationType)
    {
        return recommendationType switch
        {
            RecommendationTypes.ResourceMatch => new[]
            {
                Roles.EmergencyCoordinator, Roles.LogisticsManager, Roles.SystemAdmin
            },
            RecommendationTypes.ShelterMatch => new[]
            {
                Roles.EmergencyCoordinator, Roles.ShelterAdmin, Roles.SystemAdmin
            },
            RecommendationTypes.VolunteerMatch => new[]
            {
                Roles.EmergencyCoordinator, Roles.VolunteerCoordinator, Roles.SystemAdmin
            },
            RecommendationTypes.CloseCase => new[]
            {
                Roles.EmergencyCoordinator, Roles.CaseManager, Roles.SystemAdmin
            },
            _ => new[]
            {
                Roles.EmergencyCoordinator, Roles.CaseManager, Roles.SystemAdmin
            }
        };
    }

    public static bool CanDecide(
        string recommendationType,
        IEnumerable<string> userRoles)
    {
        var allowed = RolesFor(recommendationType);
        return userRoles.Any(role => allowed.Contains(role, StringComparer.Ordinal));
    }

    public static bool CanApproveAnnouncement(IEnumerable<string> userRoles)
    {
        return userRoles.Any(role => role is Roles.EmergencyCoordinator or Roles.SystemAdmin);
    }
}
