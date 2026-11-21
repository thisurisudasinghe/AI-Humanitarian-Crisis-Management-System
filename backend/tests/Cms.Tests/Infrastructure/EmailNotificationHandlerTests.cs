using System.Text.Json;
using Cms.Application.Contracts;
using Cms.Application.DTOs.Notifications;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Notifications;
using Cms.Infrastructure.Stubs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Infrastructure;

public class EmailNotificationHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Handler_creates_in_app_and_email_rows_for_an_active_recipient()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync("active@tests.local");
        var message = BuildMessage(user.Id, "Account.Welcome");

        await harness.Handler.HandleAsync(message);

        var rows = await harness.GetNotificationsAsync(message.Id);

        Assert.Equal(2, rows.Count);

        var inApp = Assert.Single(rows, row => row.Channel == NotificationChannel.InApp);
        Assert.Equal(NotificationStatus.Sent, inApp.Status);
        Assert.NotNull(inApp.SentAt);
        Assert.Equal("Welcome to SAFEWAY", inApp.Subject);

        var email = Assert.Single(rows, row => row.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationStatus.Sent, email.Status);
        Assert.NotNull(email.SentAt);
        Assert.Equal(1, harness.Sender.SendCount);
    }

    [Fact]
    public async Task Handler_is_idempotent_when_the_same_event_is_retried()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync("active@tests.local");
        var message = BuildMessage(user.Id, "Account.Welcome");

        await harness.Handler.HandleAsync(message);
        await harness.Handler.HandleAsync(message);

        var rows = await harness.GetNotificationsAsync(message.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal(1, harness.Sender.SendCount);
    }

    [Fact]
    public async Task Handler_marks_the_email_row_failed_and_rethrows_when_delivery_fails()
    {
        using var harness = new HandlerHarness(failingSender: true);
        var user = await harness.AddUserAsync("active@tests.local");
        var message = BuildMessage(user.Id, "Account.Welcome");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Handler.HandleAsync(message));

        var rows = await harness.GetNotificationsAsync(message.Id);

        var inApp = Assert.Single(rows, row => row.Channel == NotificationChannel.InApp);
        Assert.Equal(NotificationStatus.Sent, inApp.Status);

        var email = Assert.Single(rows, row => row.Channel == NotificationChannel.Email);
        Assert.Equal(NotificationStatus.Failed, email.Status);
        Assert.Null(email.SentAt);
    }

    [Fact]
    public async Task Handler_skips_an_inactive_recipient()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync(
            "inactive@tests.local",
            UserStatus.Inactive);
        var message = BuildMessage(user.Id, "Account.Welcome");

        await harness.Handler.HandleAsync(message);

        var rows = await harness.GetNotificationsAsync(message.Id);

        Assert.Empty(rows);
        Assert.Equal(0, harness.Sender.SendCount);
    }

    [Fact]
    public async Task Handler_creates_only_the_in_app_row_when_the_recipient_has_no_email()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync("no-email@tests.local", includeEmail: false);
        var message = BuildMessage(user.Id, "Account.Welcome");

        await harness.Handler.HandleAsync(message);

        var rows = await harness.GetNotificationsAsync(message.Id);

        var inApp = Assert.Single(rows);
        Assert.Equal(NotificationChannel.InApp, inApp.Channel);
        Assert.Equal(0, harness.Sender.SendCount);
    }

    [Fact]
    public async Task Handler_honours_an_explicit_channel()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync("active@tests.local");
        var message = BuildMessage(
            user.Id,
            "Announcement.Approved",
            channel: NotificationChannels.InApp);

        await harness.Handler.HandleAsync(message);

        var rows = await harness.GetNotificationsAsync(message.Id);

        var inApp = Assert.Single(rows);
        Assert.Equal(NotificationChannel.InApp, inApp.Channel);
        Assert.Equal(0, harness.Sender.SendCount);
    }

    [Fact]
    public async Task Handler_keeps_announcement_approved_when_a_fanout_message_is_dead_lettered()
    {
        using var harness = new HandlerHarness();
        var user = await harness.AddUserAsync("active@tests.local");
        var announcementId = Guid.NewGuid();

        await harness.SeedAnnouncementAsync(announcementId);
        await harness.SeedSiblingOutboxMessageAsync(
            announcementId,
            OutboxMessageStatus.DeadLettered);

        var message = BuildMessage(
            user.Id,
            "Announcement.Approved",
            channel: NotificationChannels.InApp,
            entityId: announcementId);

        await harness.Handler.HandleAsync(message);

        var announcement = await harness.GetAnnouncementAsync(announcementId);

        Assert.Equal(AnnouncementStatus.Approved, announcement.Status);
        Assert.Null(announcement.DispatchedAt);
    }

    private static OutboxMessage BuildMessage(
        Guid recipientUserId,
        string templateKey,
        string? channel = null,
        Guid? entityId = null)
    {
        var payload = new NotificationPayloadDto
        {
            RecipientUserId = recipientUserId,
            TemplateKey = templateKey,
            Channel = channel,
            Data = new Dictionary<string, string>
            {
                ["name"] = "Test User"
            }
        };

        return new OutboxMessage
        {
            Type = OutboxEventTypes.NotificationSend,
            EntityId = entityId ?? recipientUserId,
            Payload = JsonSerializer.Serialize(payload, JsonOptions),
            IdempotencyKey = $"notify:{Guid.NewGuid():N}",
            Status = OutboxMessageStatus.Processing
        };
    }

    private sealed class HandlerHarness : IDisposable
    {
        private readonly string _databaseName = $"cms-notify-handler-{Guid.NewGuid()}";
        private readonly ServiceProvider _provider;

        public HandlerHarness(bool failingSender = false)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["EmailProtection:EncryptionKey"] =
                        Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()),
                    ["EmailProtection:HashKey"] =
                        Convert.ToBase64String(Enumerable.Repeat((byte)13, 32).ToArray())
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDbContext<CmsDbContext>(
                options => options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<Cms.Infrastructure.Security.EmailProtectionService>();
            services.AddScoped<Cms.Application.Interfaces.IDateTimeService,
                Cms.Infrastructure.Services.DateTimeService>();
            services.AddSingleton<INotificationSender>(
                new RecordingSender(failingSender));
            services.AddScoped<InAppNotificationChannel>();
            services.AddScoped<EmailNotificationChannel>();
            services.AddScoped<INotificationChannelFactory, NotificationChannelFactory>();
            services.AddScoped<IAuditLogger, NoOpAuditLogger>();
            services.AddScoped<EmailNotificationHandler>();

            _provider = services.BuildServiceProvider();
            Sender = (RecordingSender)_provider.GetRequiredService<INotificationSender>();
            Handler = _provider.GetRequiredService<EmailNotificationHandler>();
        }

        public EmailNotificationHandler Handler { get; }

        public RecordingSender Sender { get; }

        public async Task<User> AddUserAsync(
            string email,
            UserStatus status = UserStatus.Active,
            bool includeEmail = true)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var protection = scope.ServiceProvider
                .GetRequiredService<Cms.Infrastructure.Security.EmailProtectionService>();

            var user = new User
            {
                FirstName = "Test",
                LastName = "Recipient",
                EmailEncrypted = includeEmail ? protection.EncryptEmail(email) : null,
                EmailHash = includeEmail ? protection.HashEmail(email) : null,
                PasswordHash = "unused",
                Status = status
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return user;
        }

        public async Task SeedAnnouncementAsync(Guid announcementId)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            db.Announcements.Add(new Announcement
            {
                Id = announcementId,
                Title = "Flood warning",
                Message = "Move to higher ground",
                Status = AnnouncementStatus.Approved,
                Channel = NotificationChannels.InApp,
                RequestedByUserId = Guid.NewGuid(),
                IdempotencyKey = $"announcement-test:{announcementId:N}",
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        public async Task SeedSiblingOutboxMessageAsync(
            Guid entityId,
            OutboxMessageStatus status)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = OutboxEventTypes.NotificationSend,
                EntityId = entityId,
                Payload = "{}",
                IdempotencyKey = $"announcement-sibling:{entityId:N}:{status}",
                Status = status
            });

            await db.SaveChangesAsync();
        }

        public async Task<Announcement> GetAnnouncementAsync(Guid announcementId)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            return await db.Announcements.FirstAsync(row => row.Id == announcementId);
        }

        public async Task<List<Notification>> GetNotificationsAsync(Guid outboxMessageId)
        {
            using var scope = _provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();

            return await db.Notifications
                .AsNoTracking()
                .Where(row => row.OutboxMessageId == outboxMessageId)
                .ToListAsync();
        }

        public void Dispose() => _provider.Dispose();
    }

    private sealed class RecordingSender : INotificationSender
    {
        private readonly bool _failing;

        public RecordingSender(bool failing)
        {
            _failing = failing;
        }

        public string Channel => "Email";

        public int SendCount { get; private set; }

        public Task SendAsync(
            Notification notification,
            CancellationToken cancellationToken = default)
        {
            SendCount++;

            if (_failing)
            {
                throw new InvalidOperationException("SMTP unavailable.");
            }

            return Task.CompletedTask;
        }
    }
}
