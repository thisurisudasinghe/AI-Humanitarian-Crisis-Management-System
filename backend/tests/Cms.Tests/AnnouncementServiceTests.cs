using System.Text.Json;
using Cms.Application.DTOs.Announcements;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Cms.Infrastructure.Services.Announcements;
using Moq;
using CurrentUserContract = Cms.Application.Contracts.ICurrentUser;

namespace Cms.Tests;

public class AnnouncementServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesPendingApproval_WithoutOutbox()
    {
        using var db = TestDb.Create();
        await SeedUsersAsync(db, 2);
        var service = MakeService(db, Roles.SystemAdmin);

        var announcement = await service.CreateAsync(new CreateAnnouncementRequest
        {
            Title = "Flood warning",
            Message = "Move to higher ground",
            Audience = new AnnouncementAudienceDto { AllActiveUsers = true }
        });

        Assert.Equal("PendingApproval", announcement.Status);
        Assert.Empty(db.OutboxMessages);
    }

    [Fact]
    public async Task ApproveAsync_RequiresRoleApproval_AndFansOutOnce()
    {
        using var db = TestDb.Create();
        await SeedUsersAsync(db, 2);

        var citizenService = MakeService(db, Roles.Citizen);
        var created = await citizenService.CreateAsync(new CreateAnnouncementRequest
        {
            Title = "Flood warning",
            Message = "Move to higher ground",
            Audience = new AnnouncementAudienceDto { AllActiveUsers = true }
        });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            citizenService.ApproveAsync(created.Id, new ApproveAnnouncementRequest()));

        var adminService = MakeService(db, Roles.SystemAdmin);
        var approved = await adminService.ApproveAsync(
            created.Id,
            new ApproveAnnouncementRequest { Note = "Confirmed with district office" });

        Assert.Equal("Approved", approved.Status);
        Assert.Equal(2, approved.RecipientCount);
        Assert.Equal(2, db.OutboxMessages.Count());
        Assert.All(db.OutboxMessages, message =>
        {
            Assert.Equal(OutboxTypes.NotificationSend, message.Type);
            Assert.StartsWith($"announcement:{created.Id}:", message.IdempotencyKey);

            using var payload = JsonDocument.Parse(message.Payload);
            Assert.Equal(
                NotificationChannels.InApp,
                payload.RootElement.GetProperty("channel").GetString());
        });
        Assert.Equal(2, db.OutboxMessages
            .Select(message => message.IdempotencyKey)
            .Distinct()
            .Count());

        await Assert.ThrowsAsync<ConflictException>(() =>
            adminService.ApproveAsync(created.Id, new ApproveAnnouncementRequest()));

        Assert.Equal(2, db.OutboxMessages.Count());
    }

    [Fact]
    public async Task CreateAsync_WithInvalidAudienceRole_ThrowsValidation()
    {
        using var db = TestDb.Create();
        var service = MakeService(db, Roles.SystemAdmin);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateAnnouncementRequest
            {
                Title = "Test",
                Message = "Body",
                Audience = new AnnouncementAudienceDto { Roles = new[] { "SuperUser" } }
            }));
    }

    private static async Task SeedUsersAsync(CmsDbContext db, int count)
    {
        var role = new Role { Name = Roles.Citizen };
        db.Roles.Add(role);

        for (var i = 0; i < count; i++)
        {
            db.Users.Add(new User
            {
                FirstName = $"User{i}",
                LastName = "Test",
                Status = UserStatus.Active,
                PasswordHash = "hash"
            });
        }

        await db.SaveChangesAsync();

        foreach (var user in db.Users.ToList())
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        await db.SaveChangesAsync();
    }

    private static AnnouncementService MakeService(CmsDbContext db, string role)
    {
        var currentUser = new Mock<CurrentUserContract>();
        currentUser.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(u => u.Roles).Returns(new[] { role });

        var audit = new Mock<Cms.Application.Contracts.IAuditLogger>();
        audit.Setup(a => a.LogAsync(
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AnnouncementService(
            db,
            currentUser.Object,
            audit.Object,
            new DateTimeService(),
            new OutboxService(db, new DateTimeService()));
    }
}
