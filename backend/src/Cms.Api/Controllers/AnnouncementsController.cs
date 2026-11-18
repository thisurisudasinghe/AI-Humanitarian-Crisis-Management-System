using Cms.Application.DTOs.Announcements;
using Cms.Application.Exceptions;
using Cms.Application.Interfaces.Announcements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/announcements")]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;

    public AnnouncementsController(IAnnouncementService announcementService)
    {
        _announcementService = announcementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _announcementService.GetAllAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _announcementService.GetByIdAsync(id, cancellationToken);
        return announcement == null ? NotFound() : Ok(announcement);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var announcement = await _announcementService.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, announcement);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _announcementService.ApproveAsync(id, request, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _announcementService.RejectAsync(id, request, cancellationToken));
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }
}
