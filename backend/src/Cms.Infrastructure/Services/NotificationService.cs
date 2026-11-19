using Cms.Application.Common;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>In-app inbox, read state and the admin failed-delivery view (M9).</summary>
public sealed class NotificationService : INotificationService
{
    private const int MaxInboxLimit = 200;

    private readonly CmsDbContext _db;
    private readonly IDateTimeService _dateTimeService;

    public NotificationService(CmsDbContext db, IDateTimeService dateTimeService)
    {
        _db = db;
        _dateTimeService = dateTimeService;
    }

    public async Task<NotificationInboxDto> GetInboxAsync(
        Guid recipientUserId,
        bool unreadOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);

        var normalizedLimit = Math.Clamp(limit, 1, MaxInboxLimit);

        var inbox = _db.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == recipientUserId &&
                notification.Channel == NotificationChannel.InApp);

        var unreadCount = await inbox
            .CountAsync(notification => notification.ReadAt == null, cancellationToken);

        var filtered = unreadOnly
            ? inbox.Where(notification => notification.ReadAt == null)
            : inbox;

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(normalizedLimit)
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                RecipientUserId = notification.RecipientUserId,
                Channel = notification.Channel,
                TemplateKey = notification.TemplateKey,
                Subject = notification.Subject,
                Body = notification.Body,
                Status = notification.Status,
                SentAt = notification.SentAt,
                ReadAt = notification.ReadAt,
                RequestId = notification.RequestId,
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new NotificationInboxDto
        {
            Items = items,
            UnreadCount = unreadCount,
            TotalCount = totalCount
        };
    }

    public async Task<NotificationDto> MarkReadAsync(
        Guid notificationId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == notificationId &&
                    candidate.RecipientUserId == recipientUserId &&
                    candidate.Channel == NotificationChannel.InApp,
                cancellationToken)
            ?? throw new NotFoundException(
                "Notification was not found.",
                "NOTIFICATION_NOT_FOUND");

        if (notification.ReadAt is null)
        {
            var now = _dateTimeService.UtcNow;
            notification.ReadAt = now;
            notification.UpdatedAt = now;

            await _db.SaveChangesAsync(cancellationToken);
        }

        return Map(notification);
    }

    public async Task<int> MarkAllReadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureRecipient(recipientUserId);

        var unread = await _db.Notifications
            .Where(notification =>
                notification.RecipientUserId == recipientUserId &&
                notification.Channel == NotificationChannel.InApp &&
                notification.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return 0;
        }

        var now = _dateTimeService.UtcNow;

        foreach (var notification in unread)
        {
            notification.ReadAt = now;
            notification.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return unread.Count;
    }

    public async Task<PagedResultDto<NotificationDto>> GetFailedAsync(
        NotificationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.Notifications
            .AsNoTracking()
            .Where(notification => notification.Status == NotificationStatus.Failed);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(notification => notification.UpdatedAt ?? notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                RecipientUserId = notification.RecipientUserId,
                Channel = notification.Channel,
                TemplateKey = notification.TemplateKey,
                Subject = notification.Subject,
                Body = notification.Body,
                Status = notification.Status,
                SentAt = notification.SentAt,
                ReadAt = notification.ReadAt,
                RequestId = notification.RequestId,
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<NotificationDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private static void EnsureRecipient(Guid recipientUserId)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ValidationException(
                "A notification recipient is required.",
                new Dictionary<string, string[]>
                {
                    ["recipientUserId"] = ["A notification recipient is required."]
                });
        }
    }

    private static NotificationDto Map(Notification notification) =>
        new()
        {
            Id = notification.Id,
            RecipientUserId = notification.RecipientUserId,
            Channel = notification.Channel,
            TemplateKey = notification.TemplateKey,
            Subject = notification.Subject,
            Body = notification.Body,
            Status = notification.Status,
            SentAt = notification.SentAt,
            ReadAt = notification.ReadAt,
            RequestId = notification.RequestId,
            CreatedAt = notification.CreatedAt
        };
}
