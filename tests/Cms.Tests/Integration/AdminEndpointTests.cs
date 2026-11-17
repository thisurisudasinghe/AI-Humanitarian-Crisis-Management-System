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
        Assert.True(report!.IsPlaceholder);
        Assert.Equal(DateTimeKind.Utc, report.GeneratedAt.Kind);

        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/v1/kpis?{period}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync($"/api/v1/kpis?{period}")).StatusCode);

        var invalid = await admin.GetAsync(
            "/api/v1/kpis?from=2026-11-17T00:00:00Z&to=2026-11-01T00:00:00Z");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }
}
