using System.Net;
using System.Net.Http.Json;
using Cms.Application.Access.DTOs;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Infrastructure.Access;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cms.Tests.Integration;

/// <summary>
/// API-level coverage for anonymous guest reporting, secure tracking and the
/// authenticated staff access application/review lifecycle.
/// </summary>
public sealed class AccessWorkflowTests
{
    [Fact]
    public async Task Anonymous_can_submit_and_track_guest_report_without_public_disclosure()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        using var anonymous = factory.CreateClient();
        var token = Guid.NewGuid().ToString("D");

        var created = await anonymous.PostAsJsonAsync("/api/v1/public/guest-reports", new
        {
            incidentType = "Flood",
            location = "Colombo 07",
            latitude = 6.93,
            longitude = 79.86,
            description = "Water entering a residential building",
            contactInfo = "0771234567",
            clientToken = token
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = await created.Content.ReadFromJsonAsync<GuestReportReceiptDto>();
        Assert.NotNull(receipt);
        Assert.Equal(token, receipt!.TrackingToken);

        var tracked = await anonymous.PostAsJsonAsync(
            "/api/v1/public/guest-reports/track",
            new { token });
        Assert.Equal(HttpStatusCode.OK, tracked.StatusCode);
        var trackingBody = await tracked.Content.ReadAsStringAsync();
        Assert.Contains(receipt.Reference, trackingBody, StringComparison.Ordinal);
        Assert.DoesNotContain("0771234567", trackingBody, StringComparison.Ordinal);

        var publicMap = await anonymous.GetStringAsync("/api/v1/public/map");
        var publicAlerts = await anonymous.GetStringAsync("/api/v1/public/alerts");
        Assert.DoesNotContain(receipt.Reference, publicMap, StringComparison.Ordinal);
        Assert.DoesNotContain("Water entering a residential building", publicMap, StringComparison.Ordinal);
        Assert.DoesNotContain(receipt.Reference, publicAlerts, StringComparison.Ordinal);

        var invalid = await anonymous.PostAsJsonAsync(
            "/api/v1/public/guest-reports/track",
            new { token = "not-a-token" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var stored = await db.GuestReports.SingleAsync();
        Assert.Equal(AccessKeys.HashTrackingToken(token), stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.NotEqual("0771234567", stored.ContactInfo);
    }

    [Fact]
    public async Task Coordinator_can_update_guest_status_without_exposing_the_internal_note()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        using var anonymous = factory.CreateClient();
        var token = Guid.NewGuid().ToString("D");

        var created = await anonymous.PostAsJsonAsync("/api/v1/public/guest-reports", new
        {
            incidentType = "Flood",
            location = "Colombo 07",
            description = "Coordinator review test",
            clientToken = token
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = await created.Content.ReadFromJsonAsync<GuestReportReceiptDto>();
        Assert.NotNull(receipt);

        using var coordinator = await factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CoordinatorEmail);
        var queue = await coordinator.GetFromJsonAsync<IReadOnlyList<GuestReportSummaryDto>>(
            "/api/v1/admin/guest-reports");
        Assert.NotNull(queue);
        var item = Assert.Single(queue!, report => report.Reference == receipt!.Reference);

        var update = await coordinator.PostAsJsonAsync(
            $"/api/v1/admin/guest-reports/{item.Id}/status",
            new { status = "UnderReview", note = "Internal-only coordination detail" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var tracked = await anonymous.PostAsJsonAsync(
            "/api/v1/public/guest-reports/track", new { token });
        Assert.Equal(HttpStatusCode.OK, tracked.StatusCode);
        var body = await tracked.Content.ReadAsStringAsync();
        Assert.Contains("UnderReview", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal-only coordination detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"note\"", body, StringComparison.OrdinalIgnoreCase);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        Assert.Equal(2, await db.GuestReportTimelines.CountAsync());
    }

    [Fact]
    public async Task Staff_application_requires_authentication_and_review_is_role_gated()
    {
        using var factory = new CmsWebApplicationFactory();
        await factory.EnsureSeededAsync();
        var application = new
        {
            fullName = "Test Citizen",
            email = "citizen@tests.local",
            organization = "District Office",
            designation = "Field volunteer",
            requestedRole = RoleNames.CaseManager,
            reason = "Supporting case coordination"
        };

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/v1/staff-access/applications", application)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/staff-access/applications/mine")).StatusCode);

        using var citizen = await factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/admin/staff-access-applications")).StatusCode);

        var privileged = await citizen.PostAsJsonAsync("/api/v1/staff-access/applications", new
        {
            fullName = "Test Citizen",
            email = "citizen@tests.local",
            organization = "District Office",
            designation = "Field volunteer",
            requestedRole = RoleNames.SystemAdmin,
            reason = "Testing privilege boundary"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, privileged.StatusCode);

        var created = await citizen.PostAsJsonAsync("/api/v1/staff-access/applications", application);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var receipt = await created.Content.ReadFromJsonAsync<StaffApplicationReceiptDto>();
        Assert.NotNull(receipt);

        var mine = await citizen.GetFromJsonAsync<IReadOnlyList<StaffApplicationDto>>(
            "/api/v1/staff-access/applications/mine");
        Assert.NotNull(mine);
        Assert.Contains(mine!, item => item.Reference == receipt!.Reference && item.Status == "Pending");

        using var admin = await factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);
        var reviewQueue = await admin.GetFromJsonAsync<IReadOnlyList<StaffApplicationDto>>(
            "/api/v1/admin/staff-access-applications");
        Assert.NotNull(reviewQueue);
        var pending = Assert.Single(reviewQueue!, item => item.Reference == receipt!.Reference);

        var decision = await admin.PostAsJsonAsync(
            $"/api/v1/admin/staff-access-applications/{pending.Id}/decision",
            new { approved = true });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
        var applicant = await db.Users.SingleAsync(user =>
            user.EmailHash == emailProtection.HashEmail(CmsWebApplicationFactory.CitizenEmail));
        var caseManagerRole = await db.Roles.SingleAsync(role => role.Name == RoleNames.CaseManager);
        Assert.Contains(await db.UserRoles.ToListAsync(), assignment =>
            assignment.UserId == applicant.Id && assignment.RoleId == caseManagerRole.Id);

        var updatedMine = await citizen.GetFromJsonAsync<IReadOnlyList<StaffApplicationDto>>(
            "/api/v1/staff-access/applications/mine");
        Assert.NotNull(updatedMine);
        Assert.Contains(updatedMine!, item => item.Reference == receipt!.Reference && item.Status == "Approved");
    }
}
