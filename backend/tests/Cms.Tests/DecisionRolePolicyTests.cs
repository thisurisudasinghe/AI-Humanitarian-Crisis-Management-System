using Cms.Application.Governance;
using Cms.Domain.Constants;

namespace Cms.Tests;

public class DecisionRolePolicyTests
{
    [Theory]
    [InlineData(RecommendationTypes.Classification, Roles.CaseManager, true)]
    [InlineData(RecommendationTypes.Classification, Roles.EmergencyCoordinator, true)]
    [InlineData(RecommendationTypes.Classification, Roles.SystemAdmin, false)]
    [InlineData(RecommendationTypes.ResourceMatch, Roles.LogisticsManager, true)]
    [InlineData(RecommendationTypes.ResourceMatch, Roles.EmergencyCoordinator, true)]
    [InlineData(RecommendationTypes.ResourceMatch, Roles.SystemAdmin, false)]
    [InlineData(RecommendationTypes.ShelterMatch, Roles.ShelterAdmin, true)]
    [InlineData(RecommendationTypes.VolunteerMatch, Roles.VolunteerCoordinator, true)]
    [InlineData(RecommendationTypes.Announcement, Roles.EmergencyCoordinator, true)]
    [InlineData(RecommendationTypes.Announcement, Roles.SystemAdmin, false)]
    [InlineData(RecommendationTypes.CloseCase, Roles.CaseManager, false)]
    [InlineData(RecommendationTypes.CloseCase, Roles.EmergencyCoordinator, true)]
    public void CanDecide_MatchesTheFrozenRoleMatrix(
        string type,
        string role,
        bool expected)
    {
        Assert.Equal(expected, DecisionRolePolicy.CanDecide(type, new[] { role }));
    }
}
