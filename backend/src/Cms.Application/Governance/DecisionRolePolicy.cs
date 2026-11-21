using Cms.Domain.Constants;

namespace Cms.Application.Governance;

/// <summary>
/// Exact decider matrix from the frozen Member 3 contract. Do not add break-glass
/// roles here: a critical action is only valid after one of these roles has
/// recorded a human decision.
/// </summary>
public static class DecisionRolePolicy
{
    public static IReadOnlyCollection<string> RolesFor(string recommendationType)
    {
        return recommendationType switch
        {
            RecommendationTypes.ResourceMatch => new[]
            {
                Roles.LogisticsManager, Roles.EmergencyCoordinator
            },
            RecommendationTypes.ShelterMatch => new[]
            {
                Roles.ShelterAdmin, Roles.EmergencyCoordinator
            },
            RecommendationTypes.VolunteerMatch => new[]
            {
                Roles.VolunteerCoordinator, Roles.EmergencyCoordinator
            },
            RecommendationTypes.Announcement or RecommendationTypes.CloseCase => new[]
            {
                Roles.EmergencyCoordinator
            },
            _ => new[]
            {
                Roles.CaseManager, Roles.EmergencyCoordinator
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
        return userRoles.Any(role => role is Roles.EmergencyCoordinator);
    }
}
