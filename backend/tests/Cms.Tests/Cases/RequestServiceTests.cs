using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class RequestServiceTests
{
    [Fact]
    public async Task Submit_persists_request_history_audit_and_outbox_together()
    {
        using var context = TestContext.Create();
        var user = new FakeCurrentUser();
        var now = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);
        var date = new FixedDateTimeService(now);
        var options = TestContext.Options();
        var writer = new CaseWriteHelper(context, date, options, new OutboxService(context, date));
        var policy = new OverduePolicy(options);
        var protector = TestContext.CreateProtector();
        var repository = new RequestRepository(context, policy, date);
        var service = new RequestService(
            context,
            repository,
            user,
            date,
            policy,
            protector,
            options,
            writer);

        var id = await service.SubmitAsync(new CreateRequestDto
        {
            Category = Category.Food,
            Urgency = Urgency.High,
            Description = "Need food assistance",
            Lat = 6.9m,
            Lng = 79.9m,
            HouseholdSize = 4,
            AccessibilityNeeds = "Wheelchair access"
        });

        var request = await context.Set<Request>().SingleAsync(r => r.Id == id);
        Assert.Equal(RequestStatus.Submitted, request.Status);
        Assert.Equal(now, request.SubmittedAt);
        Assert.NotEqual("Wheelchair access", request.AccessibilityNeeds);
        Assert.Equal("Wheelchair access", protector.Unprotect(request.AccessibilityNeeds));

        Assert.Equal(1, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync());
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("Request.Submitted", outbox.Type);
        Assert.Equal($"Request.Submitted:{id}:1", outbox.IdempotencyKey);
        Assert.Contains("requestId", outbox.Payload);
    }

    [Fact]
    public async Task Area_crisis_mismatch_is_rejected()
    {
        using var context = TestContext.Create();
        var crisisId = Guid.NewGuid();
        var otherCrisisId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        await context.Set<Crisis>().AddRangeAsync(
            new Crisis { Id = crisisId, Name = "A", Type = "Flood", StartedAt = DateTime.UtcNow },
            new Crisis { Id = otherCrisisId, Name = "B", Type = "Flood", StartedAt = DateTime.UtcNow });
        await context.Set<Area>().AddAsync(new Area
        {
            Id = areaId,
            CrisisId = otherCrisisId,
            Name = "Other area",
            Lat = 6m,
            Lng = 79m
        });
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var date = new FixedDateTimeService(now);
        var options = TestContext.Options();
        var writer = new CaseWriteHelper(context, date, options, new OutboxService(context, date));
        var service = new RequestService(
            context,
            new RequestRepository(context, new OverduePolicy(options), date),
            new FakeCurrentUser(),
            date,
            new OverduePolicy(options),
            TestContext.CreateProtector(),
            options,
            writer);

        var ex = await Assert.ThrowsAsync<Cms.Domain.Exceptions.ValidationException>(() =>
            service.SubmitAsync(new CreateRequestDto
            {
                Category = Category.Food,
                Urgency = Urgency.High,
                Description = "Need food assistance",
                Lat = 6.9m,
                Lng = 79.9m,
                HouseholdSize = 4,
                CrisisId = crisisId,
                AreaId = areaId
            }));

        Assert.Equal("AREA_CRISIS_MISMATCH", ex.Code);
        Assert.Equal(0, await context.Set<Request>().CountAsync());
    }

    [Fact]
    public async Task Staff_patch_assigns_request_and_detail_includes_timeline()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.Set<RequestStatusHistory>().AddAsync(new RequestStatusHistory
        {
            Id = Guid.NewGuid(),
            RequestId = request.Id,
            FromStatus = RequestStatus.Submitted,
            ToStatus = RequestStatus.Submitted,
            ChangedByUserId = request.RequesterUserId,
            CreatedAt = request.CreatedAt
        });
        await context.SaveChangesAsync();
        var service = CreateService(
            context,
            new FakeCurrentUser { Roles = new[] { CasesRoles.CaseManager } },
            DateTime.UtcNow);
        var assignee = Guid.NewGuid();

        await service.UpdateMineAsync(request.Id, new UpdateRequestDto { AssignedToUserId = assignee });

        var stored = await context.Set<Request>().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(assignee, stored.AssignedToUserId);
        Assert.Equal("Request.Assigned", (await context.AuditEvents.SingleAsync()).Action);

        var detail = await service.GetDetailAsync(request.Id);
        Assert.NotNull(detail);
        Assert.Single(detail!.Timeline);
    }

    [Fact]
    public async Task Citizen_cannot_assign_request_through_patch()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(
            context,
            new FakeCurrentUser
            {
                UserId = request.RequesterUserId,
                Roles = new[] { CasesRoles.Citizen }
            },
            DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateMineAsync(request.Id, new UpdateRequestDto { AssignedToUserId = Guid.NewGuid() }));

        Assert.Equal("STAFF_ROLE_REQUIRED", ex.Code);
        Assert.Null((await context.Set<Request>().SingleAsync(r => r.Id == request.Id)).AssignedToUserId);
    }

    [Fact]
    public async Task Citizen_edit_after_verification_is_conflict()
    {
        using var context = TestContext.Create();
        var userId = Guid.NewGuid();
        var request = TestContext.NewRequest(userId, RequestStatus.Verified);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var service = CreateService(
            context,
            new FakeCurrentUser { UserId = userId, Roles = new[] { CasesRoles.Citizen } },
            DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateMineAsync(request.Id, new UpdateRequestDto { Description = "Changed" }));

        Assert.Equal("REQUEST_NOT_EDITABLE", ex.Code);
        Assert.Equal("Test request",
            (await context.Set<Request>().SingleAsync(r => r.Id == request.Id)).Description);
    }

    private static RequestService CreateService(
        CmsDbContext context,
        FakeCurrentUser user,
        DateTime now)
    {
        var date = new FixedDateTimeService(now);
        var options = TestContext.Options();
        var policy = new OverduePolicy(options);
        return new RequestService(
            context,
            new RequestRepository(context, policy, date),
            user,
            date,
            policy,
            TestContext.CreateProtector(),
            options,
            new CaseWriteHelper(context, date, options, new OutboxService(context, date)));
    }
}
