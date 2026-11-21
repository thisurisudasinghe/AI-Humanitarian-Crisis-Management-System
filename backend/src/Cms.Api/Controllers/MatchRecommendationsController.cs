using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Required Member 3 match endpoint. The existing /matches read-only endpoint is
/// kept for compatibility; this route stores the ranked result and advances the
/// request only as far as the human-review state.
/// </summary>
[ApiController]
[Authorize(Roles =
    "CaseManager,EmergencyCoordinator,LogisticsManager,ShelterAdmin,VolunteerCoordinator,SystemAdmin")]
[Route("api/v1/recommendations")]
public sealed class MatchRecommendationsController : ControllerBase
{
    private readonly IMatchRecommendationService _matches;

    public MatchRecommendationsController(IMatchRecommendationService matches)
    {
        _matches = matches;
    }

    [HttpPost("match")]
    public async Task<IActionResult> Create(
        [FromBody] MatchRecommendationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _matches.CreateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}
