using Cms.Application.Cases.Common;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class RequestStatusServiceTests
{
    [Fact]
    public async Task Allowed_transition_writes_history_audit_and_notification_once()
    {
        using var context = TestContext.Create();
        var user = new FakeCurrentUser();
        var now = new DateTime(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, user, now);

        await service.TransitionAsync(request.Id, RequestStatus.Verified, "checked");

        var stored = await context.Set<Request>().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(RequestStatus.Verified, stored.Status);
        Assert.Equal(now, stored.VerifiedAt);
        Assert.Equal(1, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("Notification.Send", outbox.Type);
        Assert.Contains("request.status.Verified", outbox.Payload);
        Assert.Equal(request.Id, outbox.EntityId);
        Assert.Equal("Notification.Send:" + request.Id + ":1", outbox.IdempotencyKey);
        Assert.DoesNotContain("Notification.Send:", outbox.Payload);
    }

    [Fact]
    public async Task Repeating_same_transition_writes_nothing_new()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser(), now);

        await service.TransitionAsync(request.Id, RequestStatus.Verified);
        await service.TransitionAsync(request.Id, RequestStatus.Verified);

        Assert.Equal(1, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Denied_transition_returns_INVALID_TRANSITION_and_writes_nothing()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.TransitionAsync(request.Id, RequestStatus.Fulfilled));

        Assert.Equal("INVALID_TRANSITION", ex.Code);
        Assert.Equal(0, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Missing_request_returns_NOT_FOUND()
    {
        using var context = TestContext.Create();
        var service = CreateService(context, new FakeCurrentUser(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => service.TransitionAsync(Guid.NewGuid(), RequestStatus.Verified));

        Assert.Equal("REQUEST_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Critical_close_without_decision_is_forbidden()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(Guid.NewGuid(), urgency: Urgency.Critical);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.TransitionAsync(request.Id, RequestStatus.Closed));

        Assert.Equal("CLOSE_DECISION_REQUIRED", ex.Code);
        Assert.Equal(0, await context.Set<RequestStatusHistory>().CountAsync());
    }

    [Fact]
    public async Task Critical_close_with_approved_decision_succeeds()
    {
        using var context = TestContext.Create();
        var now = new DateTime(2026, 11, 18, 10, 0, 0, DateTimeKind.Utc);
        var request = TestContext.NewRequest(
            Guid.NewGuid(),
            status: RequestStatus.Fulfilled,
            urgency: Urgency.Critical);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var gate = new NoOpDecisionGate { Approved = true };
        var service = CreateService(context, new FakeCurrentUser(), now, gate);
        var decisionId = Guid.NewGuid();

        await service.TransitionAsync(request.Id, RequestStatus.Closed, "approved", decisionId);

        var stored = await context.Set<Request>().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(RequestStatus.Closed, stored.Status);
        Assert.Equal(now, stored.ClosedAt);
        Assert.Equal(1, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Merge_writes_both_histories_link_audits_and_notifications()
    {
        using var context = TestContext.Create();
        var userId = Guid.NewGuid();
        var source = TestContext.NewRequest(userId, RequestStatus.Verified);
        var target = TestContext.NewRequest(userId, RequestStatus.Triaged);
        await context.Set<Request>().AddRangeAsync(source, target);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser { UserId = userId }, DateTime.UtcNow);

        await service.MergeAsync(source.Id, target.Id, "duplicate");

        var storedSource = await context.Set<Request>().SingleAsync(r => r.Id == source.Id);
        Assert.Equal(RequestStatus.Merged, storedSource.Status);
        Assert.Equal(target.Id, storedSource.MergedIntoRequestId);
        Assert.Equal(2, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.Set<RequestLink>().CountAsync());
        Assert.Equal(2, await context.AuditEvents.CountAsync());
        Assert.Equal(2, await context.OutboxMessages.CountAsync());
        Assert.All(await context.OutboxMessages.ToListAsync(), m => Assert.Equal("Notification.Send", m.Type));
    }

    [Fact]
    public async Task Merge_rejects_self()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(Guid.NewGuid(), RequestStatus.Verified);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.MergeAsync(request.Id, request.Id));

        Assert.Equal("SELF_MERGE", ex.Code);
        Assert.Equal(0, await context.Set<RequestLink>().CountAsync());
    }

    [Fact]
    public async Task Merge_rejects_terminal_target()
    {
        using var context = TestContext.Create();
        var source = TestContext.NewRequest(Guid.NewGuid(), RequestStatus.Verified);
        var target = TestContext.NewRequest(Guid.NewGuid(), RequestStatus.Closed);
        await context.Set<Request>().AddRangeAsync(source, target);
        await context.SaveChangesAsync();
        var service = CreateService(context, new FakeCurrentUser(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => service.MergeAsync(source.Id, target.Id));

        Assert.Equal("TERMINAL_MERGE_TARGET", ex.Code);
        Assert.Equal(0, await context.Set<RequestLink>().CountAsync());
    }

    [Fact]
    public async Task Repeated_outbox_key_is_ignored()
    {
        using var context = TestContext.Create();
        var now = DateTime.UtcNow;
        var date = new FixedDateTimeService(now);
        var writer = TestContext.CreateWriter(context, date);
        var entityId = Guid.NewGuid();

        await writer.AddOutboxAsync("Test.Event", entityId, new { value = 1 }, $"Test.Event:{entityId}:1", default);
        await writer.AddOutboxAsync("Test.Event", entityId, new { value = 1 }, $"Test.Event:{entityId}:1", default);
        await context.SaveChangesAsync();

        Assert.Single(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task Unavailable_decision_gate_denies_by_default()
    {
        var gate = new UnavailableCloseCaseDecisionGate();

        Assert.False(await gate.IsApprovedAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    private static RequestStatusService CreateService(
        CmsDbContext context,
        FakeCurrentUser user,
        DateTime now,
        NoOpDecisionGate? decisionGate = null) =>
        TestContext.CreateStatusService(context, user, now, decisionGate);
}
