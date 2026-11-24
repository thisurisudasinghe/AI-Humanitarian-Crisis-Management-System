using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Authenticated public users may apply for operational staff access. The
/// requested role is review data only and grants no permission until an
/// authorized reviewer approves the application.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/staff-access/applications")]
[Produces("application/json")]
public sealed class StaffAccessController(IStaffAccessService applications) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(StaffApplicationReceiptDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Submit(
        SubmitStaffApplicationDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await applications.SubmitAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, receipt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<StaffApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await applications.ListMineAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
