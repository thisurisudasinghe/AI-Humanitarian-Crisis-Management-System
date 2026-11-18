using Cms.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

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
