using System.Text.Json;
using Cms.Application.Access.DTOs;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Access;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Access;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Access;
using Cms.Infrastructure.Data;
using Cms.Tests.Cases;
using Microsoft.EntityFrameworkCore;
using Xunit;
using AuditActions = Cms.Application.Common.AuditActions;

namespace Cms.Tests.Access;

public class AccessServiceTests
{
    [Fact]
    public async Task Guest_report_stores_only_token_hash_and_tracks_safely()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var (_, guestReports) = CreateServices(context, Guid.NewGuid(), now);
        var token = Guid.NewGuid().ToString("D");

        var receipt = await guestReports.SubmitAsync(new CreateGuestReportDto
        {
            IncidentType = "Flood",
            Location = "Colombo 07",
            Description = "Water entering homes",
            ContactInfo = "0771234567",
            ClientToken = token
        });

        Assert.Equal(token, receipt.TrackingToken);
        Assert.StartsWith("GR-", receipt.Reference);
        Assert.Equal(GuestReportStatus.Received.ToString(), receipt.Status);

        var stored = await context.GuestReports.SingleAsync();
        Assert.Equal(AccessKeys.HashTrackingToken(token), stored.TokenHash);
        Assert.NotEqual(token, stored.TokenHash);
        Assert.NotEqual("0771234567", stored.ContactInfo);

        var tracking = await guestReports.TrackAsync(token);
        Assert.Equal(receipt.Reference, tracking.Reference);
        Assert.Single(tracking.Timeline);
        Assert.Equal(GuestReportStatus.Received.ToString(), tracking.Timeline[0].Status);

        var retry = await guestReports.SubmitAsync(new CreateGuestReportDto
        {
            IncidentType = "Flood",
            Location = "Colombo 07",
            Description = "Water entering homes",
            ClientToken = token
        });
        Assert.Equal(receipt.Reference, retry.Reference);
        Assert.Single(await context.GuestReports.ToListAsync());

        await Assert.ThrowsAsync<ValidationException>(() => guestReports.TrackAsync("not-a-token"));
    }

    [Fact]
    public async Task Guest_report_rejects_out_of_range_coordinates()
    {
        using var context = TestContext.Create();
        var (_, guestReports) = CreateServices(context, Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<ValidationException>(() => guestReports.SubmitAsync(new CreateGuestReportDto
        {
            IncidentType = "Fire",
            Location = "Galle",
            Latitude = 120,
            Longitude = 79.86,
            Description = "Smoke reported",
            ClientToken = Guid.NewGuid().ToString("D")
        }));

        Assert.Empty(await context.GuestReports.ToListAsync());
    }

    [Fact]
    public async Task Guest_tracking_does_not_expose_staff_notes()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var reviewerId = Guid.NewGuid();
        var (_, guestReports) = CreateServices(context, Guid.NewGuid(), now);
        var token = Guid.NewGuid().ToString("D");

        var receipt = await guestReports.SubmitAsync(new CreateGuestReportDto
        {
            IncidentType = "Flood",
            Location = "Colombo 07",
            Description = "Water entering homes",
            ClientToken = token
        });

        var reviewUser = new FakeCurrentUser
        {
            UserId = reviewerId,
            Roles = [RoleNames.EmergencyCoordinator]
        };
        var date = new FixedDateTimeService(now);
        var reviewerService = new GuestReportService(
            context,
            reviewUser,
            date,
            TestContext.CreateProtector(),
            TestContext.CreateWriter(context, date));

        await reviewerService.UpdateStatusAsync(
            (await context.GuestReports.SingleAsync()).Id,
            new UpdateGuestReportStatusDto
            {
                Status = GuestReportStatus.UnderReview.ToString(),
                Note = "Internal coordination note that must stay private"
            });

        Assert.Equal(2, await context.GuestReportTimelines.CountAsync());

        var tracking = await guestReports.TrackAsync(token);
        var serialized = JsonSerializer.Serialize(tracking);
        Assert.DoesNotContain("Internal coordination note", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"note\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, tracking.Timeline.Count);
    }

    [Fact]
    public async Task Staff_application_rejects_system_admin_and_approval_grants_requested_role()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var applicantId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var (staffApplications, _) = CreateServices(context, applicantId, now);

        var citizenRole = new Role { Name = RoleNames.Citizen };
        var coordinatorRole = new Role { Name = RoleNames.EmergencyCoordinator };
        var applicant = new User
        {
            Id = applicantId,
            FirstName = "Applicant",
            LastName = "User",
            Status = UserStatus.Active,
            UserRoles = [new UserRole { UserId = applicantId, RoleId = citizenRole.Id }]
        };
        context.Roles.AddRange(citizenRole, coordinatorRole);
        context.Users.Add(applicant);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => staffApplications.SubmitAsync(
            new SubmitStaffApplicationDto
            {
                FullName = "Applicant User",
                Email = "applicant@example.invalid",
                Organization = "District Office",
                Designation = "Officer",
                RequestedRole = RoleNames.SystemAdmin,
                Reason = "Need access"
            }));

        var receipt = await staffApplications.SubmitAsync(new SubmitStaffApplicationDto
        {
            FullName = "Applicant User",
            Email = "applicant@example.invalid",
            Organization = "District Office",
            Designation = "Officer",
            RequestedRole = RoleNames.EmergencyCoordinator,
            Reason = "Coordinating flood response"
        });

        var stored = await context.StaffAccessApplications.SingleAsync();
        Assert.Equal(receipt.Reference, stored.Reference);
        Assert.NotEqual("Applicant User", stored.FullName);
        Assert.Equal(StaffApplicationStatus.Pending, stored.Status);

        var reviewUser = new FakeCurrentUser
        {
            UserId = reviewerId,
            Roles = [RoleNames.EmergencyCoordinator]
        };
        var writer = TestContext.CreateWriter(context, new FixedDateTimeService(now));
        var reviewerService = new StaffAccessService(
            context,
            reviewUser,
            new FixedDateTimeService(now),
            TestContext.CreateProtector(),
            writer);

        var decision = await reviewerService.DecideAsync(stored.Id, new ReviewStaffApplicationDto
        {
            Approved = true
        });

        Assert.Equal(StaffApplicationStatus.Approved.ToString(), decision.Status);
        Assert.Equal(reviewerId, stored.ReviewerUserId);
        Assert.Contains(applicant.UserRoles, role => role.RoleId == coordinatorRole.Id);
        Assert.Contains(await context.AuditEvents.ToListAsync(), audit =>
            audit.Action == AuditActions.StaffAccessApplicationApproved);
        Assert.Contains(await context.OutboxMessages.ToListAsync(), message =>
            message.Payload.Contains("StaffAccess.ApplicationApproved", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Staff_application_cannot_be_decided_by_the_applicant()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var applicantId = Guid.NewGuid();
        var (staffApplications, _) = CreateServices(context, applicantId, now);
        var applicantRole = new Role { Name = RoleNames.Citizen };
        context.Roles.Add(applicantRole);
        var applicant = new User
        {
            Id = applicantId,
            FirstName = "Applicant",
            LastName = "User",
            Status = UserStatus.Active,
            UserRoles = [new UserRole { UserId = applicantId, RoleId = applicantRole.Id }]
        };
        context.Users.Add(applicant);
        await context.SaveChangesAsync();

        var receipt = await staffApplications.SubmitAsync(new SubmitStaffApplicationDto
        {
            FullName = "Applicant User",
            Email = "applicant@example.invalid",
            Organization = "District Office",
            Designation = "Officer",
            RequestedRole = RoleNames.EmergencyCoordinator,
            Reason = "Coordinating flood response"
        });

        var stored = await context.StaffAccessApplications.SingleAsync();

        var selfReview = new FakeCurrentUser
        {
            UserId = applicantId,
            Roles = [RoleNames.EmergencyCoordinator]
        };
        var selfService = new StaffAccessService(
            context,
            selfReview,
            new FixedDateTimeService(now),
            TestContext.CreateProtector(),
            TestContext.CreateWriter(context, new FixedDateTimeService(now)));

        await Assert.ThrowsAsync<ForbiddenException>(() => selfService.DecideAsync(stored.Id, new ReviewStaffApplicationDto
        {
            Approved = true
        }));

        Assert.Equal(StaffApplicationStatus.Pending, stored.Status);
        Assert.Null(stored.ReviewedAt);
        Assert.DoesNotContain(await context.AuditEvents.ToListAsync(), audit =>
            audit.Action == AuditActions.StaffAccessApplicationApproved ||
            audit.Action == AuditActions.StaffAccessApplicationRejected);
    }

    private static (StaffAccessService StaffApplications, GuestReportService GuestReports) CreateServices(
        CmsDbContext context,
        Guid userId,
        DateTime now)
    {
        var user = new FakeCurrentUser
        {
            UserId = userId,
            Roles = [RoleNames.Citizen]
        };
        var date = new FixedDateTimeService(now);
        var writer = TestContext.CreateWriter(context, date);
        var protector = TestContext.CreateProtector();

        return (
            new StaffAccessService(context, user, date, protector, writer),
            new GuestReportService(context, user, date, protector, writer));
    }
}
