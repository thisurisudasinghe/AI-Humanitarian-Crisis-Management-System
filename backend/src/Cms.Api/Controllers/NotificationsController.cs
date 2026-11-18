using Cms.Application.Contracts;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>In-app notification inbox for the authenticated user (M9).</summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    /// <summary>Lists the caller's in-app notifications, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(NotificationInboxDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationInboxDto>> GetInbox(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _notificationService.GetInboxAsync(
            CurrentUserId(),
            unreadOnly,
            limit,
            cancellationToken));
    }

    /// <summary>Marks one of the caller's notifications as read.</summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _notificationService.MarkReadAsync(
            id,
            CurrentUserId(),
            cancellationToken));
    }

    /// <summary>Marks every unread notification as read and returns the count.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> MarkAllRead(CancellationToken cancellationToken)
    {
        var updated = await _notificationService.MarkAllReadAsync(
            CurrentUserId(),
            cancellationToken);

        return Ok(new { updated });
    }

    private Guid CurrentUserId() =>
        _currentUser.UserId
        ?? throw new UnauthorizedAccessException("Authentication is required.");
}
