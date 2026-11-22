
using Cms.Application.Interfaces.AI;
using Cms.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DecisionController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public DecisionController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    public record RecordDecisionRequest(
        Guid RecommendationId,
        DecisionOutcome Outcome,
        string Reason,
        string? FinalPayloadJson = null);

    [HttpPost]
    public async Task<IActionResult> RecordDecision(
        [FromBody] RecordDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var decisionId = await _decisionService.RecordDecisionAsync(
                request.RecommendationId,
                request.Outcome,
                request.Reason,
                request.FinalPayloadJson,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetDecisions),
                new { recommendationId = request.RecommendationId },
                new { decisionId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [HttpGet("recommendation/{recommendationId:guid}")]
    public async Task<IActionResult> GetDecisions(
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var decisions = await _decisionService.GetDecisionsAsync(
                recommendationId,
                cancellationToken);

            return Ok(decisions);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
