using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs.Notifications;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Integration;

public sealed class NotificationEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public NotificationEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Inbox_requires_authentication()
    {
        await ResetNotificationsAsync();

        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inbox_returns_only_the_callers_in_app_notifications()
    {
        await ResetNotificationsAsync();

        var citizenId = (await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail)).UserId;
        var adminId = (await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail)).UserId;

        await AddNotificationAsync(citizenId, NotificationChannel.InApp, "Citizen message");
        await AddNotificationAsync(citizenId, NotificationChannel.InApp, "Citizen message 2",
            readAt: DateTime.UtcNow);
        await AddNotificationAsync(citizenId, NotificationChannel.Email, "Citizen email");
        await AddNotificationAsync(adminId, NotificationChannel.InApp, "Admin message");

        using var client = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var response = await client.GetAsync("/api/v1/notifications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inbox = await response.Content.ReadFromJsonAsync<NotificationInboxDto>(JsonOptions);
        Assert.NotNull(inbox);

        Assert.Equal(2, inbox!.Items.Count);
        Assert.All(inbox.Items, item => Assert.Equal(citizenId, item.RecipientUserId));
        Assert.All(inbox.Items, item => Assert.Equal(NotificationChannel.InApp, item.Channel));
        Assert.Equal(1, inbox.UnreadCount);
        Assert.Equal(2, inbox.TotalCount);
    }

    [Fact]
    public async Task Mark_read_marks_only_notifications_owned_by_the_caller()
    {
        await ResetNotificationsAsync();

        var citizenId = (await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail)).UserId;
        var adminId = (await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail)).UserId;

        var own = await AddNotificationAsync(
            citizenId, NotificationChannel.InApp, "Mine");
        var other = await AddNotificationAsync(
            adminId, NotificationChannel.InApp, "Not mine");

        using var client = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var ownResponse = await client.PatchAsync(
            $"/api/v1/notifications/{own}/read",
            content: null);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);

        var marked = await ownResponse.Content.ReadFromJsonAsync<NotificationDto>(JsonOptions);
        Assert.NotNull(marked);
        Assert.NotNull(marked!.ReadAt);

        var otherResponse = await client.PatchAsync(
            $"/api/v1/notifications/{other}/read",
            content: null);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task Mark_all_read_marks_every_unread_notification_for_the_caller()
    {
        await ResetNotificationsAsync();

        var citizenId = (await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail)).UserId;
        var adminId = (await _factory.LoginAsync(CmsWebApplicationFactory.AdminEmail)).UserId;

        await AddNotificationAsync(citizenId, NotificationChannel.InApp, "One");
        await AddNotificationAsync(citizenId, NotificationChannel.InApp, "Two");
        var adminNotification = await AddNotificationAsync(
            adminId, NotificationChannel.InApp, "Admin");

        using var client = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var response = await client.PostAsync("/api/v1/notifications/read-all", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, document.RootElement.GetProperty("updated").GetInt32());

        var inbox = await client.GetFromJsonAsync<NotificationInboxDto>(
            "/api/v1/notifications",
            JsonOptions);
        Assert.NotNull(inbox);
        Assert.Equal(0, inbox!.UnreadCount);

        var adminStillUnread = await GetNotificationAsync(adminNotification);
        Assert.Null(adminStillUnread.ReadAt);
    }

    [Fact]
    public async Task Admin_failed_view_requires_admin_and_returns_only_failed_rows()
    {
        await ResetNotificationsAsync();

        var citizenId = (await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail)).UserId;

        await AddNotificationAsync(
            citizenId, NotificationChannel.Email, "Failed one",
            status: NotificationStatus.Failed);
        await AddNotificationAsync(
            citizenId, NotificationChannel.Email, "Sent one",
            status: NotificationStatus.Sent);
        await AddNotificationAsync(
            citizenId, NotificationChannel.InApp, "In-app failed",
            status: NotificationStatus.Failed);

        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        var forbidden = await citizen.GetAsync("/api/v1/admin/notifications/failed");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);
        var response = await admin.GetAsync("/api/v1/admin/notifications/failed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<
            Cms.Application.DTOs.PagedResultDto<NotificationDto>>(JsonOptions);
        Assert.NotNull(page);

        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(NotificationStatus.Failed, item.Status));
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task ResetNotificationsAsync()
    {
        await _factory.EnsureSeededAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        db.Notifications.RemoveRange(db.Notifications);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> AddNotificationAsync(
        Guid recipientUserId,
        NotificationChannel channel,
        string subject,
        NotificationStatus status = NotificationStatus.Sent,
        DateTime? readAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            Channel = channel,
            TemplateKey = "Test.Notification",
            Subject = subject,
            Body = subject,
            Status = status,
            SentAt = status == NotificationStatus.Sent ? DateTime.UtcNow : null,
            ReadAt = readAt,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return notification.Id;
    }

    private async Task<Notification> GetNotificationAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

        return await db.Notifications
            .AsNoTracking()
            .SingleAsync(row => row.Id == id);
    }
}
