using Cms.Infrastructure.Cases;

namespace Cms.Tests.Cases;

public class OverduePolicyTests
{
    [Fact]
    public void Exactly_at_SLA_boundary_is_not_overdue()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var policy = new OverduePolicy(TestContext.Options());

        Assert.False(policy.IsOverdue(now.AddHours(-24), now));
        Assert.Equal(24, policy.AgeHours(now.AddHours(-24), now));
    }

    [Fact]
    public void One_minute_past_SLA_is_overdue()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
        var policy = new OverduePolicy(TestContext.Options());

        Assert.True(policy.IsOverdue(now.AddHours(-24).AddMinutes(-1), now));
    }
}
