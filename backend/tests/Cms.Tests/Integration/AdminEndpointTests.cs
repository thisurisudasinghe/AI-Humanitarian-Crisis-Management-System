using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.Common;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Admin;
using Cms.Application.DTOs.Audit;
using Cms.Application.DTOs.Kpi;
using Cms.Domain.Constants;
using Cms.Domain.Enums;

namespace Cms.Tests.Integration;

public sealed class AdminEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public AdminEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/organisations")]
    [InlineData("/api/v1/admin/config")]
    [InlineData("/api/v1/audit-events")]
    public async Task Admin_endpoints_reject_anonymous_callers(string path)
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/organisations")]
    [InlineData("/api/v1/admin/config")]
    [InlineData("/api/v1/audit-events")]
    public async Task Admin_endpoints_reject_non_admin_callers(string path)
    {
        await _factory.EnsureSeededAsync();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);
        using var officer = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.OfficerEmail);

        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_assign_roles_and_deactivate_a_user()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"staff.{suffix}@tests.local";

        var createResponse = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequestDto
        {
            FullName = "Case Manager Test",
            Email = email,
            Password = TestSecrets.Password,
            Roles = [RoleNames.CaseManager]
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Contains(RoleNames.CaseManager, created!.Roles);
        Assert.Equal(email, created.Email);
        Assert.True(created.IsActive);

        var duplicate = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequestDto
        {
            FullName = "Duplicate Staff",
            Email = email.ToUpperInvariant(),
            Password = TestSecrets.Password,
            Roles = [RoleNames.CaseManager]
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var emailSearch = await admin.GetAsync(
            $"/api/v1/users?search={Uri.EscapeDataString(email.ToUpperInvariant())}&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, emailSearch.StatusCode);
        var emailResults = await emailSearch.Content.ReadFromJsonAsync<PagedResultDto<UserSummaryDto>>(JsonOptions);
        Assert.NotNull(emailResults);
        Assert.Contains(emailResults!.Items, item => item.Id == created.Id);

        var rolesResponse = await admin.PutAsJsonAsync(
            $"/api/v1/users/{created.Id}/roles",
            new ReplaceRolesRequestDto
            {
                Roles = [RoleNames.CaseManager, RoleNames.EmergencyCoordinator]
            });

        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var withRoles = await rolesResponse.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions);
        Assert.NotNull(withRoles);
        Assert.Equal(2, withRoles!.Roles.Count);

        var deactivateResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{created.Id}",
            new UpdateUserRequestDto { Status = UserStatus.Inactive });

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        var deactivated = await deactivateResponse.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions);
        Assert.NotNull(deactivated);
        Assert.False(deactivated!.IsActive);

        var auditResponse = await admin.GetAsync(
            $"/api/v1/audit-events?entityType=identity.User&entityId={created.Id}&page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(audit);
        Assert.True(audit!.TotalCount >= 3);
        Assert.DoesNotContain(audit.Items, item => item.Action == AuditActions.UserRegistered && item.Details?.Contains(email) == true);
        Assert.Contains(audit.Items, item => item.Action == AuditActions.UserRoleChanged);
        Assert.Contains(audit.Items, item => item.Action == AuditActions.UserDeactivated);
    }

    [Fact]
    public async Task Admin_can_create_update_and_audit_an_organisation()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var createResponse = await admin.PostAsJsonAsync("/api/v1/organisations", new CreateOrganisationRequestDto
        {
            Name = $"Test Partner {Guid.NewGuid():N}",
            OrganisationType = OrganisationType.NonGovernmentalOrganisation,
            ContactEmail = "contact@partner.test"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<OrganisationDto>(JsonOptions);
        Assert.NotNull(created);

        var patchResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/organisations/{created!.Id}",
            new UpdateOrganisationRequestDto { IsActive = false });

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var patched = await patchResponse.Content.ReadFromJsonAsync<OrganisationDto>(JsonOptions);
        Assert.NotNull(patched);
        Assert.False(patched!.IsActive);

        var auditResponse = await admin.GetAsync(
            $"/api/v1/audit-events?entityType=common.Organisation&entityId={created.Id}");

        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!.Items, item => item.Action == AuditActions.OrganisationCreated);
        Assert.Contains(audit.Items, item => item.Action == AuditActions.OrganisationDeactivated);
    }

    [Fact]
    public async Task Config_updates_are_audited_and_read_back()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var patchResponse = await admin.PatchAsJsonAsync("/api/v1/admin/config", new UpdateConfigRequestDto
        {
            Settings =
            [
                new SystemSettingValueDto { Key = SystemSettingKeys.SlaHours, Value = "24" },
                new SystemSettingValueDto { Key = SystemSettingKeys.ReorderThreshold, Value = "10" }
            ]
        });

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        var getResponse = await admin.GetAsync("/api/v1/admin/config");
        var settings = await getResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SystemSettingDto>>();
        Assert.NotNull(settings);
        Assert.Contains(settings!, setting => setting.Key == SystemSettingKeys.SlaHours && setting.Value == "24");
        Assert.Contains(settings!, setting => setting.Key == SystemSettingKeys.ReorderThreshold && setting.Value == "10");

        var auditResponse = await admin.GetAsync(
            $"/api/v1/audit-events?action={AuditActions.ConfigUpdated}&entityType=common.SystemSetting&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(audit);
        Assert.NotEmpty(audit!.Items);
        Assert.All(audit.Items, item => Assert.Equal(AuditActions.ConfigUpdated, item.Action));
    }

    [Fact]
    public async Task Kpi_endpoint_enforces_roles_and_date_validation()
    {
        await _factory.EnsureSeededAsync();
        var period = "from=2026-11-01T00:00:00Z&to=2026-11-17T00:00:00Z";

        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        using var officer = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.OfficerEmail);
        using var citizen = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);

        var adminResponse = await admin.GetAsync($"/api/v1/kpis?{period}");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var report = await adminResponse.Content.ReadFromJsonAsync<KpiReportDto>();
        Assert.NotNull(report);
        Assert.False(report!.IsPlaceholder);
        Assert.Equal(DateTimeKind.Utc, report.GeneratedAt.Kind);

        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/v1/kpis?{period}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync($"/api/v1/kpis?{period}")).StatusCode);

        var invalid = await admin.GetAsync(
            "/api/v1/kpis?from=2026-11-17T00:00:00Z&to=2026-11-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/users")]
    [InlineData("PUT", "/api/v1/users/00000000-0000-0000-0000-000000000001/roles")]
    [InlineData("PATCH", "/api/v1/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/organisations")]
    [InlineData("PATCH", "/api/v1/organisations/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/v1/admin/config")]
    public async Task Admin_write_endpoints_reject_non_admin_callers(string method, string path)
    {
        await _factory.EnsureSeededAsync();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);
        using var officer = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.OfficerEmail);

        using var citizenRequest = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        };
        using var officerRequest = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        };

        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.SendAsync(citizenRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await officer.SendAsync(officerRequest)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_users_and_organisations_by_id_and_unknown_ids_return_404()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var users = await admin.GetAsync("/api/v1/users?page=1&pageSize=5");
        var userPage = await users.Content.ReadFromJsonAsync<PagedResultDto<UserSummaryDto>>(JsonOptions);
        Assert.NotNull(userPage);
        var firstUser = Assert.Single(userPage!.Items.Take(1));

        var userResponse = await admin.GetAsync($"/api/v1/users/{firstUser.Id}");
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        var user = await userResponse.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions);
        Assert.NotNull(user);
        Assert.Equal(firstUser.Id, user!.Id);

        var missingUser = await admin.GetAsync($"/api/v1/users/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingUser.StatusCode);

        var organisations = await admin.GetAsync("/api/v1/organisations?page=1&pageSize=5");
        var organisationPage = await organisations.Content.ReadFromJsonAsync<PagedResultDto<OrganisationDto>>(JsonOptions);
        Assert.NotNull(organisationPage);
        var firstOrganisation = Assert.Single(organisationPage!.Items.Take(1));

        var organisationResponse = await admin.GetAsync($"/api/v1/organisations/{firstOrganisation.Id}");
        Assert.Equal(HttpStatusCode.OK, organisationResponse.StatusCode);
        var organisation = await organisationResponse.Content.ReadFromJsonAsync<OrganisationDto>(JsonOptions);
        Assert.NotNull(organisation);
        Assert.Equal(firstOrganisation.Id, organisation!.Id);

        var missingOrganisation = await admin.GetAsync($"/api/v1/organisations/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missingOrganisation.StatusCode);
    }

    [Fact]
    public async Task Replacing_roles_with_an_unknown_role_returns_422()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var createResponse = await admin.PostAsJsonAsync("/api/v1/users", new CreateUserRequestDto
        {
            FullName = "Role Validation User",
            Email = $"role.validation.{Guid.NewGuid():N}@tests.local",
            Password = TestSecrets.Password,
            Roles = [RoleNames.Citizen]
        });

        var created = await createResponse.Content.ReadFromJsonAsync<UserSummaryDto>(JsonOptions);
        Assert.NotNull(created);

        var response = await admin.PutAsJsonAsync(
            $"/api/v1/users/{created!.Id}/roles",
            new ReplaceRolesRequestDto { Roles = ["SuperWizard"] });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDATION_FAILED", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Audit_query_filters_by_action_actor_and_date_and_pages_newest_first()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        var adminAuth = await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail);

        await admin.PatchAsJsonAsync("/api/v1/admin/config", new UpdateConfigRequestDto
        {
            Settings =
            [
                new SystemSettingValueDto { Key = SystemSettingKeys.SlaHours, Value = "24" }
            ]
        });

        await admin.PostAsJsonAsync("/api/v1/organisations", new CreateOrganisationRequestDto
        {
            Name = $"Audit Filter Org {Guid.NewGuid():N}",
            OrganisationType = OrganisationType.Other
        });

        var actionResponse = await admin.GetAsync(
            $"/api/v1/audit-events?action={AuditActions.ConfigUpdated}&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, actionResponse.StatusCode);
        var actionPage = await actionResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(actionPage);
        Assert.NotEmpty(actionPage!.Items);
        Assert.All(actionPage.Items, item => Assert.Equal(AuditActions.ConfigUpdated, item.Action));

        var actorResponse = await admin.GetAsync(
            $"/api/v1/audit-events?actorUserId={adminAuth.UserId}&page=1&pageSize=50");
        var actorPage = await actorResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(actorPage);
        Assert.NotEmpty(actorPage!.Items);
        Assert.All(actorPage.Items, item => Assert.Equal(adminAuth.UserId, item.ActorUserId));

        var now = DateTime.UtcNow;
        var dateResponse = await admin.GetAsync(
            $"/api/v1/audit-events?from={Uri.EscapeDataString(now.AddHours(-1).ToString("O"))}" +
            $"&to={Uri.EscapeDataString(now.AddHours(1).ToString("O"))}&page=1&pageSize=50");
        var datePage = await dateResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(datePage);
        Assert.NotEmpty(datePage!.Items);

        var futureResponse = await admin.GetAsync(
            $"/api/v1/audit-events?from={Uri.EscapeDataString(now.AddDays(1).ToString("O"))}");
        var futurePage = await futureResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(futurePage);
        Assert.Empty(futurePage!.Items);

        var firstPageResponse = await admin.GetAsync("/api/v1/audit-events?page=1&pageSize=1");
        var secondPageResponse = await admin.GetAsync("/api/v1/audit-events?page=2&pageSize=1");
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        Assert.True(firstPage!.TotalCount >= 2);
        Assert.NotEqual(firstPage.Items.Single().Id, secondPage!.Items.Single().Id);
        Assert.True(firstPage.Items.Single().CreatedAt >= secondPage.Items.Single().CreatedAt);
    }

    [Fact]
    public async Task Kpi_endpoint_rejects_anonymous_callers_and_echoes_filters_as_utc()
    {
        await _factory.EnsureSeededAsync();
        using var anonymous = _factory.CreateClient();

        var anonymousResponse = await anonymous.GetAsync(
            "/api/v1/kpis?from=2026-11-01T00:00:00Z&to=2026-11-30T00:00:00Z");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var crisisId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        var response = await admin.GetAsync(
            $"/api/v1/kpis?from=2026-11-01T00:00:00&to=2026-11-30T00:00:00" +
            $"&crisisEventId={crisisId}&areaId={areaId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<KpiReportDto>();
        Assert.NotNull(report);
        Assert.Equal(crisisId, report!.CrisisEventId);
        Assert.Equal(areaId, report.AreaId);
        Assert.Equal(DateTimeKind.Utc, report.From.Kind);
        Assert.Equal(DateTimeKind.Utc, report.To.Kind);
        Assert.Empty(report.RequestsByArea);
        Assert.Empty(report.SheltersByArea);
    }

    [Fact]
    public async Task Admin_cannot_remove_own_admin_role_or_deactivate_self()
    {
        await _factory.EnsureSeededAsync();
        var adminAuth = await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail);
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var removeOwnAdmin = await admin.PutAsJsonAsync(
            $"/api/v1/users/{adminAuth.UserId}/roles",
            new ReplaceRolesRequestDto { Roles = [RoleNames.Citizen] });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, removeOwnAdmin.StatusCode);

        var deactivateSelf = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{adminAuth.UserId}",
            new UpdateUserRequestDto { Status = UserStatus.Inactive });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deactivateSelf.StatusCode);

        var users = await admin.GetAsync(
            $"/api/v1/users?search={Uri.EscapeDataString(CmsWebApplicationFactory.AdminEmail)}");
        users.EnsureSuccessStatusCode();
        var page = await users.Content.ReadFromJsonAsync<PagedResultDto<UserSummaryDto>>(JsonOptions);
        Assert.NotNull(page);
        var stillAdmin = Assert.Single(page!.Items);
        Assert.True(stillAdmin.IsActive);
        Assert.Contains(RoleNames.SystemAdmin, stillAdmin.Roles);
    }

    [Fact]
    public async Task Admin_cannot_suspend_self()
    {
        await _factory.EnsureSeededAsync();
        var adminAuth = await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail);
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        var suspendSelf = await admin.PatchAsJsonAsync(
            $"/api/v1/users/{adminAuth.UserId}",
            new UpdateUserRequestDto { Status = UserStatus.Suspended });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, suspendSelf.StatusCode);

        var users = await admin.GetAsync(
            $"/api/v1/users?search={Uri.EscapeDataString(CmsWebApplicationFactory.AdminEmail)}");
        users.EnsureSuccessStatusCode();
        var page = await users.Content.ReadFromJsonAsync<PagedResultDto<UserSummaryDto>>(JsonOptions);
        Assert.NotNull(page);
        Assert.True(Assert.Single(page!.Items).IsActive);
    }
}
