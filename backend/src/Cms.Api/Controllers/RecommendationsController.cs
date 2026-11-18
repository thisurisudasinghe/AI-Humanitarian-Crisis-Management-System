using Cms.Application.DTOs.AI;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/recommendations")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationService _recommendationService;

    public RecommendationsController(IRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _recommendationService.GetAllAsync(type, cancellationToken);
        return Ok(items.Select(Map));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _recommendationService.GetByIdAsync(id, cancellationToken);
        return item == null ? NotFound() : Ok(Map(item));
    }

    [HttpGet("target/{targetId:guid}")]
    public async Task<IActionResult> GetByTarget(
        Guid targetId,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        var items = await _recommendationService.GetByTargetAsync(
            targetId,
            type,
            cancellationToken);
        return Ok(items.Select(Map));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = await _recommendationService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id }, new { id });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("matches")]
    public async Task<IActionResult> GetMatches(
        [FromBody] MatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var matches = await _recommendationService.GetMatchesAsync(
                request,
                cancellationToken);
            return Ok(matches);
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

    private static RecommendationDto Map(Cms.Domain.Entities.Recommendation r) => new()
    {
        Id = r.Id,
        Type = r.Type,
        TargetId = r.TargetId,
        PayloadJson = r.PayloadJson,
        Confidence = r.Confidence,
        Rationale = r.Rationale,
        Source = r.Source,
        CreatedAt = r.CreatedAt
    };
}
