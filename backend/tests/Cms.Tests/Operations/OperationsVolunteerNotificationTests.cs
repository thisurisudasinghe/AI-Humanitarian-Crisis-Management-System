using Cms.Application.Contracts;
using Cms.Application.DTOs.Notifications;
using Cms.Domain.Entities.Operations;
using Cms.Domain.Enums.Cases;
using Moq;

namespace Cms.Tests.Operations;

/// <summary>Volunteer notification regression coverage for the operations outbox.</summary>
public class OperationsVolunteerNotificationTests
{
    private sealed record EnqueuedNotification(string Type, Guid EntityId, object Payload, string IdempotencyKey);

    [Fact]
    public async Task VolunteerTaskLifecycle_AssignRespondComplete_EnqueuesThreeDistinctNotificationsWithRecipients()
    {
        var (h, outbox, auditActions) = CreateRecordingHarness();
        await SeedAssignableVolunteerAsync(h);

        await h.Service.AssignVolunteerTaskAsync(OpsIds.Task, OpsIds.Volunteer, CancellationToken.None);
        await h.Service.RespondToTaskAsync(OpsIds.Task, true, null, CancellationToken.None);
        await h.Service.CompleteTaskAsync(OpsIds.Task, CancellationToken.None);

        Assert.Equal(3, outbox.Count);
        Assert.All(outbox, row => Assert.Equal(OutboxEventTypes.NotificationSend, row.Type));
        Assert.All(outbox, row => Assert.Equal(OpsIds.Task, row.EntityId));
        Assert.Equal(3, outbox.Select(row => row.IdempotencyKey).Distinct().Count());
        Assert.DoesNotContain("Notification.Skipped", auditActions);

        var expectedActions = new[] { "task-assigned", "task-response", "task-completed" };
        var baseKey = OutboxEventTypes.BuildIdempotencyKey(OutboxEventTypes.NotificationSend, OpsIds.Task, 1);
        for (var i = 0; i < expectedActions.Length; i++)
        {
            var notification = Assert.IsType<NotificationPayloadDto>(outbox[i].Payload);
            Assert.Equal(OpsIds.User, notification.RecipientUserId);
            Assert.Equal($"Operations.{expectedActions[i]}", notification.TemplateKey);
            Assert.Equal(expectedActions[i], notification.Data["action"]);
            Assert.Equal(OpsIds.User.ToString(), notification.Data["userId"]);
            Assert.Equal($"{baseKey}:{expectedActions[i]}", outbox[i].IdempotencyKey);
        }
    }

    [Fact]
    public async Task VerifyVolunteer_ApprovalAndRejection_UseDistinctIdempotencyKeys()
    {
        var (h, outbox, auditActions) = CreateRecordingHarness();
        h.Db.Volunteers.Add(new Volunteer
        {
            Id = OpsIds.Volunteer,
            UserId = OpsIds.User,
            VerificationStatus = "Pending"
        });
        await h.Db.SaveChangesAsync();

        await h.Service.VerifyVolunteerAsync(OpsIds.Volunteer, true, "Documents checked", CancellationToken.None);
        await h.Service.VerifyVolunteerAsync(OpsIds.Volunteer, false, "Documents expired", CancellationToken.None);

        Assert.Equal(2, outbox.Count);
        Assert.All(outbox, row => Assert.Equal(OutboxEventTypes.NotificationSend, row.Type));
        Assert.All(outbox, row => Assert.Equal(OpsIds.Volunteer, row.EntityId));
        Assert.DoesNotContain("Notification.Skipped", auditActions);

        var approval = Assert.IsType<NotificationPayloadDto>(outbox[0].Payload);
        var rejection = Assert.IsType<NotificationPayloadDto>(outbox[1].Payload);
        Assert.Equal(OpsIds.User, approval.RecipientUserId);
        Assert.Equal(OpsIds.User, rejection.RecipientUserId);

        var baseKey = OutboxEventTypes.BuildIdempotencyKey(OutboxEventTypes.NotificationSend, OpsIds.Volunteer, 1);
        Assert.Equal($"{baseKey}:volunteer-verification-Verified", outbox[0].IdempotencyKey);
        Assert.Equal($"{baseKey}:volunteer-verification-Rejected", outbox[1].IdempotencyKey);
        Assert.NotEqual(outbox[0].IdempotencyKey, outbox[1].IdempotencyKey);
    }

    [Fact]
    public async Task UnsuffixedEvent_KeepsTheExistingIdempotencyKeyExactly()
    {
        var (h, outbox, auditActions) = CreateRecordingHarness();
        h.Db.Requests.Add(OpsSeed.Request(OpsIds.Task, Urgency.Critical));
        h.Db.AssistanceRequests.Add(new AssistanceRequest
        {
            Id = OpsIds.Task,
            FromOrgId = OpsIds.Org,
            ToOrgId = OpsIds.OtherOrg,
            ResourceTypeId = OpsIds.FoodType,
            Quantity = 3,
            Reason = "Supplies needed",
            Status = "Pending"
        });
        await h.Db.SaveChangesAsync();

        await h.Service.RespondAssistanceRequestAsync(OpsIds.Task, true, "Approved", CancellationToken.None);

        var row = Assert.Single(outbox);
        Assert.DoesNotContain("Notification.Skipped", auditActions);
        Assert.Equal(OutboxEventTypes.BuildIdempotencyKey(OutboxEventTypes.NotificationSend, OpsIds.Task, 1), row.IdempotencyKey);
        var notification = Assert.IsType<NotificationPayloadDto>(row.Payload);
        Assert.Equal(OpsIds.User, notification.RecipientUserId);
    }

    private static (OpsHarness Harness, List<EnqueuedNotification> Outbox, List<string> AuditActions) CreateRecordingHarness()
    {
        var h = OpsHarness.Create();
        var outbox = new List<EnqueuedNotification>();
        h.Outbox.Setup(o => o.EnqueueAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<object>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Guid, object, string, CancellationToken>((type, entityId, payload, key, _) =>
                outbox.Add(new EnqueuedNotification(type, entityId, payload, key)))
            .Returns(Task.CompletedTask);

        var auditActions = new List<string>();
        h.Audit.Setup(a => a.LogAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, string, string?, object?, CancellationToken>((_, action, _, _, _, _) =>
                auditActions.Add(action))
            .Returns(Task.CompletedTask);

        return (h, outbox, auditActions);
    }

    private static async Task SeedAssignableVolunteerAsync(OpsHarness h)
    {
        h.Db.Volunteers.Add(new Volunteer
        {
            Id = OpsIds.Volunteer,
            UserId = OpsIds.User,
            Active = true,
            VerificationStatus = "Verified"
        });
        h.Db.VolunteerAvailabilities.Add(new VolunteerAvailability
        {
            Id = Guid.NewGuid(),
            VolunteerId = OpsIds.Volunteer,
            StartsAt = DateTime.UtcNow.AddHours(-1),
            EndsAt = DateTime.UtcNow.AddHours(1)
        });
        h.Db.VolunteerTasks.Add(new VolunteerTask
        {
            Id = OpsIds.Task,
            Title = "Deliver supplies",
            Status = "Open"
        });
        await h.Db.SaveChangesAsync();
    }
}
