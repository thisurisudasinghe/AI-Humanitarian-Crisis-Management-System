using Cms.Api.Controllers;
using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Domain.Enums.Cases;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Cms.Tests.Cases;

public class RequestsControllerTransitionResponseTests
{
    [Fact]
    public async Task Decision_gated_critical_close_returns_200()
    {
        var requestId = Guid.NewGuid();
        var decisionId = Guid.NewGuid();
        var requests = new Mock<IRequestService>();
        requests
            .Setup(service => service.GetDetailAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestDetailDto { Urgency = Urgency.Critical });
        var workflow = new Mock<IRequestWorkflowService>();
        workflow
            .Setup(service => service.TransitionAsync(
                requestId,
                It.IsAny<TransitionRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateController(requests, workflow);

        var result = await controller.Transition(
            requestId,
            new TransitionRequestDto { To = RequestStatus.Closed, DecisionId = decisionId },
            CancellationToken.None);

        Assert.IsType<OkResult>(result);
        workflow.Verify(
            service => service.TransitionAsync(
                requestId,
                It.Is<TransitionRequestDto>(body => body.DecisionId == decisionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Ordinary_transition_returns_204_without_extra_read()
    {
        var requestId = Guid.NewGuid();
        var requests = new Mock<IRequestService>();
        var workflow = new Mock<IRequestWorkflowService>();
        workflow
            .Setup(service => service.TransitionAsync(
                requestId,
                It.IsAny<TransitionRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateController(requests, workflow);

        var result = await controller.Transition(
            requestId,
            new TransitionRequestDto { To = RequestStatus.Verified },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        requests.Verify(
            service => service.GetDetailAsync(requestId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Close_with_decision_but_non_critical_urgency_returns_204()
    {
        var requestId = Guid.NewGuid();
        var requests = new Mock<IRequestService>();
        requests
            .Setup(service => service.GetDetailAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RequestDetailDto { Urgency = Urgency.High });
        var workflow = new Mock<IRequestWorkflowService>();
        workflow
            .Setup(service => service.TransitionAsync(
                requestId,
                It.IsAny<TransitionRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateController(requests, workflow);

        var result = await controller.Transition(
            requestId,
            new TransitionRequestDto { To = RequestStatus.Closed, DecisionId = Guid.NewGuid() },
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    private static RequestsController CreateController(
        Mock<IRequestService> requests,
        Mock<IRequestWorkflowService> workflow) =>
        new(requests.Object, workflow.Object, Mock.Of<IRequestMergeService>(), Mock.Of<IAttachmentService>());
}
