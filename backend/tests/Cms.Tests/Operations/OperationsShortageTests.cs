using Cms.Application.Common;
using Cms.Application.DTOs;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cms.Tests.Operations;

/// <summary>D4-12 item 2: side-effect-free shortage ranking and explanation rules.</summary>
public class OperationsShortageTests
{
    [Fact]
    public async Task GetShortageProposal_ThreeUrgentRequests_RankStablyAndExplainPartialFulfilment()
    {
        var h = OpsHarness.Create();
        var older = DateTime.UtcNow.AddDays(-4);
        var newer = DateTime.UtcNow.AddDays(-2);
        var wrongCategory = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.Add(OpsSeed.Inventory(5));
        h.Db.Requests.AddRange(
            OpsSeed.Request(OpsIds.Request, Urgency.Critical, created: older),
            OpsSeed.Request(OpsIds.RequestB, Urgency.Critical, created: newer,
                accessibility: "wheelchair access needed"),
            OpsSeed.Request(OpsIds.RequestC, Urgency.High, created: older.AddDays(-1)),
            OpsSeed.Request(wrongCategory, Urgency.Critical, category: Category.Medicine));
        await h.Db.SaveChangesAsync();
        SetupReader(h, new Dictionary<Guid, RequestSummaryDto>
        {
            [OpsIds.Request] = OpsSeed.Summary(OpsIds.Request, Urgency.Critical, created: older),
            [OpsIds.RequestB] = OpsSeed.Summary(OpsIds.RequestB, Urgency.Critical, created: newer,
                accessibility: "wheelchair access needed"),
            [OpsIds.RequestC] = OpsSeed.Summary(OpsIds.RequestC, Urgency.High, created: older.AddDays(-1)),
            [wrongCategory] = OpsSeed.Summary(wrongCategory, Urgency.Critical, category: Category.Medicine)
        });

        var first = await h.Service.GetShortageProposalAsync(OpsIds.FoodType, CancellationToken.None);
        var second = await h.Service.GetShortageProposalAsync(OpsIds.FoodType, CancellationToken.None);

        Assert.Equal(5, first.AvailableQuantity);
        Assert.False(first.HasSideEffects);
        Assert.True(first.HumanApprovalRequired);
        Assert.Equal(new[] { OpsIds.RequestB, OpsIds.Request, OpsIds.RequestC },
            first.Items.Select(x => x.RequestId));
        Assert.Equal(new[] { 1, 2, 3 }, first.Items.Select(x => x.PriorityPosition));
        Assert.Equal(new[] { 3m, 2m, 0m }, first.Items.Select(x => x.ProposedPartialFulfilment));
        Assert.All(first.Items, item => Assert.Equal(5, item.AvailableAtStart));
        Assert.Contains("accessibility need", first.Items[0].Explanation);
        Assert.Contains("partial fulfilment", first.Items[1].Explanation);
        Assert.Contains("human decision is required", first.Items[2].Explanation);
        Assert.DoesNotContain(first.Items, item => item.RequestId == wrongCategory);
        Assert.Equal(first.Items, second.Items);

        // The proposal reserves nothing and changes no status.
        Assert.Equal(5, (await h.Db.Inventories.SingleAsync()).Available);
        Assert.Empty(await h.Db.Allocations.ToListAsync());
        Assert.All(await h.Db.Requests.ToListAsync(), r =>
            Assert.True(r.Status is RequestStatus.Submitted or RequestStatus.Triaged));
    }

    [Fact]
    public async Task GetShortageProposal_ExactlyEnoughStock_ProposesFullFulfilment()
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        h.Db.Inventories.Add(OpsSeed.Inventory(5));
        h.Db.Requests.Add(OpsSeed.Request(OpsIds.Request, Urgency.Critical,
            description: "5 packs"));
        await h.Db.SaveChangesAsync();
        SetupReader(h, new Dictionary<Guid, RequestSummaryDto>
        {
            [OpsIds.Request] = OpsSeed.Summary(OpsIds.Request, Urgency.Critical, description: "5 packs")
        });

        var proposal = await h.Service.GetShortageProposalAsync(OpsIds.FoodType, CancellationToken.None);

        var item = Assert.Single(proposal.Items);
        Assert.Equal(5, item.RequestedQuantity);
        Assert.Equal(5, item.ProposedPartialFulfilment);
        Assert.Contains("enough of the available 5 packs", item.Explanation);
        Assert.Contains("human decision is required", item.Explanation);
        Assert.Equal(5, (await h.Db.Inventories.SingleAsync()).Available);
    }

    [Fact]
    public async Task GetShortageProposal_NoRequests_ReturnsEmptyProposal()
    {
        var h = OpsHarness.Create();
        h.Db.ResourceTypes.Add(OpsSeed.FoodType());
        await h.Db.SaveChangesAsync();
        SetupReader(h, new Dictionary<Guid, RequestSummaryDto>());

        var proposal = await h.Service.GetShortageProposalAsync(OpsIds.FoodType, CancellationToken.None);

        Assert.Empty(proposal.Items);
        Assert.Equal(0, proposal.AvailableQuantity);
        Assert.False(proposal.HasSideEffects);
        Assert.True(proposal.HumanApprovalRequired);
    }

    [Fact]
    public async Task GetShortageProposal_MissingResourceType_IsRejected()
    {
        var h = OpsHarness.Create();

        var empty = await Assert.ThrowsAsync<ValidationException>(
            () => h.Service.GetShortageProposalAsync(Guid.Empty, CancellationToken.None));
        Assert.Equal("A resource type is required.", empty.Message);

        var missing = await Assert.ThrowsAsync<NotFoundException>(
            () => h.Service.GetShortageProposalAsync(OpsIds.FoodType, CancellationToken.None));
        Assert.Equal("RESOURCE_TYPE_NOT_FOUND", missing.Code);
    }

    private static void SetupReader(OpsHarness h, IReadOnlyDictionary<Guid, RequestSummaryDto> summaries) =>
        h.RequestReader.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult(
                summaries.TryGetValue(id, out var summary) ? summary : null));
}
