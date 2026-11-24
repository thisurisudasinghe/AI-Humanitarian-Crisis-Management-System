using Cms.Api.Constants;
using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Review queue for staff access applications. Access is limited to the
/// explicitly listed coordinator/admin reviewer roles.
/// </summary>
[ApiController]
[Authorize(Roles = AuthorizedRoles.StaffAccessReviewers)]
[Route("api/v1/admin/staff-access-applications")]
[Produces("application/json")]
public sealed class AdminStaffAccessController(IStaffAccessService applications) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StaffApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await applications.ListForReviewAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType(typeof(StaffApplicationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Decide(
        Guid id,
        ReviewStaffApplicationDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await applications.DecideAsync(id, request, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
