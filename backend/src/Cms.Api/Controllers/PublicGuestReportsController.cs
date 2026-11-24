using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

/// <summary>
/// Anonymous emergency reporting with a high-entropy bearer tracking token.
/// The token is supplied in the POST body and is never placed in a URL.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public")]
[Route("api/v1/public/guest-reports")]
[Produces("application/json")]
public sealed class PublicGuestReportsController(IGuestReportService reports) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(GuestReportReceiptDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        CreateGuestReportDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await reports.SubmitAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, receipt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("track")]
    [ProducesResponseType(typeof(GuestReportTrackingDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Track(
        TrackGuestReportDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await reports.TrackAsync(request.Token, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
