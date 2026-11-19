using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Cases;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class RequestReaderTests
{
    [Fact]
    public async Task GetAsync_maps_all_summary_fields_without_tracking()
    {
        using var context = TestContext.Create();
        var protector = TestContext.CreateProtector();
        var created = new DateTime(2026, 11, 17, 10, 30, 0, DateTimeKind.Utc);
        var request = new Request
        {
            Id = Guid.NewGuid(),
            RequesterUserId = Guid.NewGuid(),
            AreaId = Guid.NewGuid(),
            Category = Category.Medicine,
            Urgency = Urgency.Critical,
            Status = RequestStatus.Triaged,
            Description = "Need medicine",
            Lat = 6.812345m,
            Lng = 79.912345m,
            HouseholdSize = 5,
            AccessibilityNeeds = protector.Protect("Wheelchair"),
            SubmittedAt = created,
            CreatedAt = created
        };
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new RequestReader(context, protector).GetAsync(request.Id);

        Assert.NotNull(result);
        Assert.Equal(request.Id, result!.Id);
        Assert.Equal(Category.Medicine, result.Category);
        Assert.Equal(Urgency.Critical, result.Urgency);
        Assert.Equal(RequestStatus.Triaged, result.Status);
        Assert.Equal("Need medicine", result.Description);
        Assert.Equal(6.812345m, result.Lat);
        Assert.Equal(79.912345m, result.Lng);
        Assert.Equal(request.AreaId, result.AreaId);
        Assert.Equal(5, result.HouseholdSize);
        Assert.Equal("Wheelchair", result.AccessibilityNeeds);
        Assert.Equal(created, result.CreatedAt);
        Assert.Empty(context.ChangeTracker.Entries<Request>());
    }

    [Fact]
    public async Task GetAsync_returns_null_for_missing_request()
    {
        using var context = TestContext.Create();
        var result = await new RequestReader(context, TestContext.CreateProtector()).GetAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task Merged_source_request_still_resolves()
    {
        using var context = TestContext.Create();
        var targetId = Guid.NewGuid();
        var source = TestContext.NewRequest(Guid.NewGuid(), RequestStatus.Merged);
        source.MergedIntoRequestId = targetId;
        await context.Set<Request>().AddAsync(source);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await new RequestReader(context, TestContext.CreateProtector()).GetAsync(source.Id);

        Assert.NotNull(result);
        Assert.Equal(RequestStatus.Merged, result!.Status);
    }
}
