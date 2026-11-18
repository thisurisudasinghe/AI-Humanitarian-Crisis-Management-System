using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class RequestRepositoryTests
{
    [Fact]
    public async Task Overdue_filter_excludes_terminal_requests()
    {
        using var context = TestContext.Create();
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var old = now.AddHours(-25);
        await context.Set<Request>().AddRangeAsync(
            TestContext.NewRequest(Guid.NewGuid(), submittedAt: old),
            TestContext.NewRequest(Guid.NewGuid(), RequestStatus.Closed, submittedAt: old),
            TestContext.NewRequest(Guid.NewGuid(), submittedAt: now.AddHours(-1)));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context, now);

        var result = await repository.ListAsync(
            new RequestFilter { Overdue = true },
            new PagedQuery());

        Assert.Equal(1, result.Total);
        Assert.Equal(RequestStatus.Submitted, Assert.Single(result.Items).Status);
    }

    [Fact]
    public async Task Urgency_sort_uses_contract_priority_order()
    {
        using var context = TestContext.Create();
        await context.Set<Request>().AddRangeAsync(
            TestContext.NewRequest(Guid.NewGuid(), urgency: Urgency.Low),
            TestContext.NewRequest(Guid.NewGuid(), urgency: Urgency.Critical),
            TestContext.NewRequest(Guid.NewGuid(), urgency: Urgency.Medium),
            TestContext.NewRequest(Guid.NewGuid(), urgency: Urgency.High));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context, DateTime.UtcNow);

        var result = await repository.ListAsync(
            new RequestFilter(),
            new PagedQuery { SortBy = "urgency", SortDescending = true });

        Assert.Equal(
            new[] { Urgency.Critical, Urgency.High, Urgency.Medium, Urgency.Low },
            result.Items.Select(r => r.Urgency));
    }

    private static RequestRepository CreateRepository(
        ApplicationDbContext context,
        DateTime now)
    {
        var options = TestContext.Options();
        return new RequestRepository(context, new OverduePolicy(options), new FixedDateTimeService(now));
    }
}
