using Cms.Domain.Entities.Operations;
using Cms.Infrastructure.Services.Operations;
namespace Cms.Tests;
public class OperationsScoringTests
{
    [Fact]
    public void WeightedStrategy_IsDeterministic_AndRewardsHealthyAvailableStock()
    {
        var strategy=new WeightedResourceMatchScoringStrategy();
        var inventory=new Inventory{Quantity=100,Available=80,ReorderThreshold=10,ExpiryDate=DateTime.UtcNow.AddDays(60)};
        var first=strategy.Score(inventory);
        var second=strategy.Score(inventory);
        Assert.Equal(first.Score,second.Score);
        Assert.InRange(first.Score,0m,1m);
        Assert.Contains("60%",first.Rationale);
    }
}
