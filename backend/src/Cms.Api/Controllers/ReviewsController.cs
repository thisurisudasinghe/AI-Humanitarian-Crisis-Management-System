using Cms.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>Read-only post-incident review over recommendations and human decisions.</summary>
/// <remarks>
/// Exposes AI output beside the recorded outcome, override flag and final request
/// status. Results use the shared paged `{items,total,page,pageSize}` shape.
/// </remarks>
[ApiController]
[Authorize]
[Route("api/v1/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly IPostIncidentReviewService _reviewService;

    public ReviewsController(IPostIncidentReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Lists review rows, optionally filtered by crisis, date range and outcome.</summary>
    [HttpGet("decisions")]
    public async Task<IActionResult> GetDecisions(
        [FromQuery] Guid? crisisId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetDecisionsAsync(
            crisisId,
            page,
            pageSize,
            cancellationToken);
        return Ok(result);
    }
}
