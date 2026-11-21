using Cms.Application.DTOs;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>SystemAdmin view of failed notification deliveries (M9).</summary>
[ApiController]
[Route("api/v1/admin/notifications")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public sealed class AdminNotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public AdminNotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Lists notifications whose delivery failed, newest first.</summary>
    [HttpGet("failed")]
    [ProducesResponseType(typeof(PagedResultDto<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<NotificationDto>>> GetFailed(
        [FromQuery] NotificationQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.GetFailedAsync(query, cancellationToken));
    }
}
