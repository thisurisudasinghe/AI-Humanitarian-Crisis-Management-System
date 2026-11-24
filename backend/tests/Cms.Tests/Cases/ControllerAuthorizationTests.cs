using System.Reflection;
using Cms.Api.Controllers;
using Cms.Domain.Cases;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Cms.Tests.Cases;

public class ControllerAuthorizationTests
{
    [Fact]
    public void Module_role_sets_match_the_frozen_contract()
    {
        Assert.Equal(RoleNames.Citizen, CasesRoles.Citizen);
        Assert.Equal(RoleNames.EmergencyCoordinator, CasesRoles.EmergencyCoordinator);
        Assert.Equal(RoleNames.CaseManager, CasesRoles.CaseManager);
        Assert.Equal(RoleNames.ShelterAdmin, CasesRoles.ShelterAdmin);
        Assert.Equal(RoleNames.PartnerOfficer, CasesRoles.PartnerOfficer);
        Assert.Equal(RoleNames.SystemAdmin, CasesRoles.SystemAdmin);
        Assert.Equal($"{RoleNames.EmergencyCoordinator},{RoleNames.CaseManager}", CasesRoles.CaseStaff);
        Assert.Equal(RoleNames.EmergencyCoordinator, CasesRoles.CoordinatorOnly);
        Assert.DoesNotContain(RoleNames.SystemAdmin, CasesRoles.CaseStaff.Split(','));
        Assert.DoesNotContain(RoleNames.SystemAdmin, CasesRoles.CoordinatorOnly.Split(','));
    }

    [Fact]
    public void Request_routes_use_citizen_owner_and_case_staff_roles()
    {
        AssertRoles<RequestsController>("Submit", CasesRoles.Citizen);
        AssertRoles<RequestsController>("Mine", CasesRoles.Citizen);
        AssertRoles<RequestsController>("GetById", CasesRoles.RequestReaders);
        AssertRoles<RequestsController>("Update", CasesRoles.RequestReaders);
        AssertRoles<RequestsController>("Timeline", CasesRoles.RequestReaders);
        AssertRoles<RequestsController>("AddFeedback", CasesRoles.Citizen);
        AssertRoles<RequestsController>("ListStaff", CasesRoles.CaseStaff);
        AssertRoles<RequestsController>("Overdue", CasesRoles.CaseStaff);
        AssertRoles<RequestsController>("Unresolved", CasesRoles.CaseStaff);
        AssertRoles<RequestsController>("Transition", CasesRoles.CaseStaff);
        AssertRoles<RequestsController>("Assign", CasesRoles.CaseStaff);
        AssertRoles<RequestsController>("Merge", CasesRoles.CaseStaff);
    }

    [Fact]
    public void Crisis_writes_use_coordinator_only_and_public_routes_are_anonymous()
    {
        AssertRoles<CrisesController>("Create", CasesRoles.CoordinatorOnly);
        AssertRoles<CrisesController>("Update", CasesRoles.CoordinatorOnly);
        AssertRoles<CrisesController>("CreateArea", CasesRoles.CoordinatorOnly);
        AssertRoles<CrisesController>("CreateAlert", CasesRoles.CoordinatorOnly);
        AssertRoles<AlertsController>("Update", CasesRoles.CoordinatorOnly);
        AssertRoles<AlertsController>("List", CasesRoles.CoordinatorOnly);
        AssertRoles<AlertsController>("PublishToMap", CasesRoles.CoordinatorOnly);
        AssertRoles<ServiceCentresController>("SetPublicVisibility", CasesRoles.CoordinatorOnly);

        var publicController = typeof(PublicController);
        Assert.NotNull(publicController.GetCustomAttribute<AllowAnonymousAttribute>());
        var publicMapController = typeof(PublicMapController);
        Assert.NotNull(publicMapController.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public void Public_participation_writes_require_verified_email()
    {
        AssertPolicy<RequestsController>("Submit", "VerifiedOnly");
        AssertPolicy<PublicPortalController>("Enroll", "VerifiedOnly");
        AssertPolicy<PublicPortalController>("RecordDonation", "VerifiedOnly");
        AssertPolicy<OperationsController>("SaveVolunteerProfile", "VerifiedOnly");
        AssertPolicy<OperationsController>("AddAvailability", "VerifiedOnly");
        AssertPolicy<OperationsController>("RespondToTask", "VerifiedOnly");
        AssertPolicy<OperationsController>("CompleteTask", "VerifiedOnly");
    }

    [Fact]
    public void Public_reads_and_auth_status_do_not_require_verified_email()
    {
        AssertNoVerifiedPolicy<PublicPortalController>("VolunteerStatus");
        AssertNoVerifiedPolicy<PublicPortalController>("DonorProfile");
        AssertNoVerifiedPolicy<PublicPortalController>("DonorCampaigns");
        AssertNoVerifiedPolicy<PublicPortalController>("DonorDonations");
        AssertNoVerifiedPolicy<PublicPortalController>("DonorDonation");
        AssertNoVerifiedPolicy<PublicPortalController>("Transparency");
        AssertNoVerifiedPolicy<OperationsController>("MyVolunteerTasks");
        AssertNoVerifiedPolicy<AuthController>("Me");
    }

    [Fact]
    public void Registration_and_verification_endpoints_remain_anonymous()
    {
        AssertAnonymous<AuthController>("Register");
        AssertAnonymous<AuthController>("Login");
        AssertAnonymous<AuthController>("Refresh");
        AssertAnonymous<AuthController>("VerifyEmail");
        AssertAnonymous<AuthController>("ResendVerification");
    }

    private static void AssertRoles<TController>(string methodName, string expectedRoles)
    {
        var method = typeof(TController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"{typeof(TController).Name}.{methodName} was not found.");
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedRoles, authorize!.Roles);
    }

    private static void AssertPolicy<TController>(string methodName, string expectedPolicy)
    {
        var method = typeof(TController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"{typeof(TController).Name}.{methodName} was not found.");
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal(expectedPolicy, authorize!.Policy);
    }

    private static void AssertNoVerifiedPolicy<TController>(string methodName)
    {
        var method = typeof(TController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"{typeof(TController).Name}.{methodName} was not found.");
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotEqual("VerifiedOnly", authorize?.Policy);
    }

    private static void AssertAnonymous<TController>(string methodName)
    {
        var method = typeof(TController).GetMethod(methodName)
            ?? throw new InvalidOperationException($"{typeof(TController).Name}.{methodName} was not found.");
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }
}
