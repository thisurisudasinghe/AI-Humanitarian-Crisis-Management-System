using Cms.Api.Constants;
using Cms.Application.DTOs.Governance;
using Cms.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GovernanceDecisionService = Cms.Application.Interfaces.Governance.IDecisionService;

namespace Cms.Api.Controllers;

/// <summary>
/// Exposes the exact singular decision route required by the frozen contract.
/// The plural route remains available for existing clients.
/// </summary>
[ApiController]
[Authorize(Roles = AuthorizedRoles.Deciders)]
[Route("api/v1/recommendations")]
public sealed class DecisionAliasController : ControllerBase
{
    private readonly GovernanceDecisionService _decisionService;

    public DecisionAliasController(GovernanceDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpPost("{recommendationId:guid}/decision")]
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
}
