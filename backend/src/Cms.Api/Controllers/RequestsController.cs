using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/requests")]
public class RequestsController : ControllerBase
{
    private readonly IRequestService _requests;
    private readonly IRequestWorkflowService _workflow;
    private readonly IRequestMergeService _merge;
    private readonly IAttachmentService _attachments;

    public RequestsController(
        IRequestService requests,
        IRequestWorkflowService workflow,
        IRequestMergeService merge,
        IAttachmentService attachments)
    {
        _requests = requests;
        _workflow = workflow;
        _merge = merge;
        _attachments = attachments;
    }

    [HttpPost]
    [Authorize(Roles = CasesRoles.Citizen)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit([FromBody] CreateRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await _requests.SubmitAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("mine")]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> Mine([FromQuery] PagedQuery query, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.ListMineAsync(query, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("{id:guid}", Name = "GetRequestById")]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _requests.GetDetailAsync(id, cancellationToken);
            return result is null ? CasesProblem.From(new Domain.Exceptions.NotFoundException("REQUEST_NOT_FOUND", $"Request {id} was not found.")) : Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            await _requests.UpdateMineAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("{id:guid}/timeline")]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> Timeline(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.GetTimelineAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = CasesRoles.Citizen)]
    [RequestSizeLimit(5L * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var attachmentId = await _attachments.UploadAsync(id, new UploadedFile
            {
                Content = stream,
                FileName = file.FileName,
                ContentType = file.ContentType
            }, cancellationToken);
            return CreatedAtAction(nameof(DownloadAttachment), new { id, attachmentId }, new { attachmentId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> DownloadAttachment(
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var download = await _attachments.GetForDownloadAsync(id, attachmentId, cancellationToken);
            return File(download.Content, download.ContentType, download.OriginalFileName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/feedback")]
    [Authorize(Roles = CasesRoles.Citizen)]
    public async Task<IActionResult> AddFeedback(
        Guid id,
        [FromBody] CreateFeedbackDto feedback,
        CancellationToken cancellationToken)
    {
        try
        {
            var feedbackId = await _requests.AddFeedbackAsync(id, feedback, cancellationToken);
            return Ok(new { id = feedbackId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> ListStaff(
        [FromQuery] RequestFilter filter,
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.ListStaffAsync(filter, query, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("overdue")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Overdue(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _requests.ListStaffAsync(new RequestFilter { Overdue = true }, query, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("unresolved")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Unresolved(
        [FromQuery] PagedQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var openStatuses = Enum.GetValues<Domain.Enums.Cases.RequestStatus>()
                .Where(Cms.Domain.Cases.RequestStateMachine.IsOpen)
                .ToList();
            return Ok(await _requests.ListStaffAsync(
                new RequestFilter { Statuses = openStatuses },
                query,
                cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/transition")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Transition(
        Guid id,
        [FromBody] TransitionRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _workflow.TransitionAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _requests.AssignAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/merge")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Merge(
        Guid id,
        [FromBody] MergeRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _merge.MergeAsync(id, request.TargetRequestId, request.Reason, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
