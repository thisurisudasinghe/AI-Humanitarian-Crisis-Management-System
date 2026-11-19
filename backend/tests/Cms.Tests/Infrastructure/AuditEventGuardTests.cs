using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Infrastructure;

public class AuditEventGuardTests
{
    private static CmsDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<CmsDbContext>()
            .UseInMemoryDatabase($"cms-audit-{Guid.NewGuid()}")
            .Options);

    private static AuditEvent NewEvent() => new()
    {
        Action = "User.LoggedIn",
        EntityType = "User",
        EntityId = Guid.NewGuid().ToString(),
        ActorUserId = Guid.NewGuid(),
        Details = """{"Email":"[REDACTED]"}"""
    };

    [Fact]
    public async Task Audit_event_can_be_inserted()
    {
        await using var context = CreateContext();

        context.AuditEvents.Add(NewEvent());
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Audit_event_cannot_be_modified()
    {
        await using var context = CreateContext();
        var auditEvent = NewEvent();
        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync();

        auditEvent.Action = "User.Deleted";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_event_cannot_be_deleted()
    {
        await using var context = CreateContext();
        var auditEvent = NewEvent();
        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync();

        context.AuditEvents.Remove(auditEvent);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
    }
}
