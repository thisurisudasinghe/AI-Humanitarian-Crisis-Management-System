using Cms.Api.Constants;
using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Internal operational queue for anonymous emergency reports. Guest reports
/// are never published through the anonymous map or alert feeds.
/// </summary>
[ApiController]
[Authorize(Roles = AuthorizedRoles.Deciders)]
[Route("api/v1/admin/guest-reports")]
[Produces("application/json")]
public sealed class AdminGuestReportsController(IGuestReportService reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GuestReportSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.ListForOperationsAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(GuestReportTrackingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        UpdateGuestReportStatusDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.UpdateStatusAsync(id, request, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
