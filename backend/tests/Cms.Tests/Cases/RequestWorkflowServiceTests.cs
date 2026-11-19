using Cms.Application.Cases.DTOs;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class RequestWorkflowServiceTests
{
    [Fact]
    public async Task Critical_close_with_approved_decision_delegates_to_status_service()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(
            Guid.NewGuid(),
            status: RequestStatus.Fulfilled,
            urgency: Urgency.Critical);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var statusService = TestContext.CreateStatusService(
            context,
            new FakeCurrentUser(),
            DateTime.UtcNow,
            new NoOpDecisionGate { Approved = true });
        var workflow = new RequestWorkflowService(statusService);

        await workflow.TransitionAsync(request.Id, new TransitionRequestDto
        {
            To = RequestStatus.Closed,
            Reason = "decision approved",
            DecisionId = Guid.NewGuid()
        });

        var stored = await context.Set<Request>().SingleAsync(r => r.Id == request.Id);
        Assert.Equal(RequestStatus.Closed, stored.Status);
        Assert.Equal(1, await context.Set<RequestStatusHistory>().CountAsync());
        Assert.Equal(1, await context.AuditEvents.CountAsync());
        Assert.Equal(1, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Critical_close_without_decision_id_is_forbidden()
    {
        using var context = TestContext.Create();
        var request = TestContext.NewRequest(
            Guid.NewGuid(),
            status: RequestStatus.Fulfilled,
            urgency: Urgency.Critical);
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        var statusService = TestContext.CreateStatusService(
            context,
            new FakeCurrentUser(),
            DateTime.UtcNow,
            new NoOpDecisionGate { Approved = true });
        var workflow = new RequestWorkflowService(statusService);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            workflow.TransitionAsync(request.Id, new TransitionRequestDto { To = RequestStatus.Closed }));

        Assert.Equal("CLOSE_DECISION_REQUIRED", ex.Code);
        Assert.Equal(RequestStatus.Fulfilled,
            (await context.Set<Request>().SingleAsync(r => r.Id == request.Id)).Status);
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }
}
