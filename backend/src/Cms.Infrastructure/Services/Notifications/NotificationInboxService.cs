using Cms.Application.DTOs.Common;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Notifications;

public class NotificationInboxService : INotificationInboxService
{
    private readonly ApplicationDbContext _context;

    public NotificationInboxService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<NotificationDto>> GetInboxAsync(
        Guid userId,
        int page = 1,
        int pageSize = 20,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.Channel == "InApp");

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Channel = n.Channel,
                Status = n.Status.ToString(),
                CreatedAt = n.CreatedAt,
                SentAt = n.SentAt,
                ReadAt = n.ReadAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationDto>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _context.Notifications.CountAsync(
            n => n.UserId == userId &&
                 n.Channel == "InApp" &&
                 n.Status == NotificationStatus.Sent &&
                 n.ReadAt == null,
            cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.UserId == userId,
            cancellationToken);

        if (notification == null)
        {
            return false;
        }

        notification.ReadAt ??= DateTime.UtcNow;
        notification.UpdatedAt = notification.ReadAt;
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
