using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationInboxService _inboxService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(
        INotificationInboxService inboxService,
        ICurrentUserService currentUser)
    {
        _inboxService = inboxService;
        _currentUser = currentUser;
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> GetInbox(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _inboxService.GetInboxAsync(
            userId,
            page,
            pageSize,
            unreadOnly,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var count = await _inboxService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var updated = await _inboxService.MarkAsReadAsync(userId, id, cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}
