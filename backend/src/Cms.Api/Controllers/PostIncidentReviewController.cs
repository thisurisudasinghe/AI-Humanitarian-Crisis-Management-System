using Cms.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Exact required post-incident review route. The existing /reviews/decisions
/// route remains as a compatibility alias.
/// </summary>
[ApiController]
[Authorize(Roles =
    "EmergencyCoordinator,CaseManager,LogisticsManager,ShelterAdmin,VolunteerCoordinator,SystemAdmin")]
[Route("api/v1")]
public sealed class PostIncidentReviewController : ControllerBase
{
    private readonly IPostIncidentReviewService _reviewService;

    public PostIncidentReviewController(IPostIncidentReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet("post-incident-review")]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? crisisId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? outcome = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _reviewService.GetDecisionsAsync(
            crisisId,
            page,
            pageSize,
            cancellationToken,
            from,
            to,
            outcome);
        return Ok(result);
    }
}
