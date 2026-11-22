using Cms.Api.Constants;
using Cms.Application.DTOs.AI;
using Cms.Application.Interfaces.AI;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AuthorizedRoles.Staff)]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationController(
        IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        var recommendationId = await _recommendationService.CreateAsync(
            request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { recommendationId },
            new { id = recommendationId });
    }

    [HttpGet("{recommendationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        var recommendation = await _recommendationService.GetByIdAsync(
            recommendationId, cancellationToken);

        if (recommendation is null)
        {
            return NotFound();
        }

        return Ok(recommendation);
    }

    /// <summary>Generates an advisory crisis summary for EmergencyCoordinator review.</summary>
    [Authorize(Roles = Roles.EmergencyCoordinator)]
    [HttpPost("crisis/{crisisId:guid}/generate")]
    public async Task<IActionResult> GenerateForCrisis(
        Guid crisisId,
        CancellationToken cancellationToken)
    {
        var recommendationId =
            await _recommendationService.GenerateForCrisisAsync(
                crisisId, cancellationToken);

        return Ok(new
        {
            recommendationId,
            message = "Crisis recommendation generated successfully."
        });
    }
}