using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public class DecisionsController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public DecisionsController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpPost("recommendations/{recommendationId:guid}/decisions")]
    public async Task<IActionResult> Record(
        Guid recommendationId,
        [FromBody] DecisionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var decision = await _decisionService.RecordAsync(
                recommendationId,
                request,
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, decision);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
        catch (ConflictException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet("recommendations/{recommendationId:guid}/decisions")]
    public async Task<IActionResult> GetByRecommendation(
        Guid recommendationId,
        CancellationToken cancellationToken = default)
    {
        var decisions = await _decisionService.GetByRecommendationAsync(
            recommendationId,
            cancellationToken);
        return Ok(decisions);
    }

    [HttpGet("decisions/{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var decision = await _decisionService.GetByIdAsync(id, cancellationToken);
        return decision == null ? NotFound() : Ok(decision);
    }
}
