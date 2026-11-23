using Cms.Domain.Constants;

namespace Cms.Tests.Domain;

public class RoleNamesTests
{
    [Fact]
    public void All_contains_exactly_the_supported_roles()
    {
        var expected = new[]
        {
            "Citizen",
            "EmergencyCoordinator",
            "CaseManager",
            "LogisticsManager",
            "ShelterAdmin",
            "VolunteerCoordinator",
            "Volunteer",
            "DonorOfficer",
            "PartnerOfficer",
            "SystemAdmin",
            "Donor"
        };

        Assert.Equal(11, RoleNames.All.Count);
        Assert.Equal(expected.Order(), RoleNames.All.Order());
    }

    [Fact]
    public void All_entries_are_unique_and_not_blank()
    {
        Assert.Equal(RoleNames.All.Count, RoleNames.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(RoleNames.All, role => Assert.False(string.IsNullOrWhiteSpace(role)));
    }

    [Fact]
    public void Legacy_administrator_role_names_are_not_used()
    {
        Assert.DoesNotContain("ShelterAdministrator", RoleNames.All);
        Assert.DoesNotContain("PartnerOrganisationOfficer", RoleNames.All);
        Assert.DoesNotContain("SystemAdministrator", RoleNames.All);
    }
}
