using System.Text.Json;
using Cms.Application.DTOs.Announcements;
using Cms.Application.DTOs.Common;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Exceptions;
using Cms.Application.Governance;
using Cms.Application.Interfaces;
using Cms.Application.Interfaces.Announcements;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Announcements;

public sealed class AnnouncementService : IAnnouncementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _auditService;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOutbox _outbox;

    public AnnouncementService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService auditService,
        IDateTimeService dateTimeService,
        IOutbox outbox)
    {
        _context = context;
        _currentUser = currentUser;
        _auditService = auditService;
        _dateTimeService = dateTimeService;
        _outbox = outbox;
    }

    public async Task<AnnouncementDto> CreateAsync(
        CreateAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title) ||
            string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Title and message are required.");
        }

        if (_currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("An authenticated user is required.");
        }

        var audience = request.Audience ?? new AnnouncementAudienceDto();
        ValidateAudience(audience);

        var channel = string.IsNullOrWhiteSpace(request.Channel)
            ? NotificationChannels.InApp
            : request.Channel.Trim();

        if (channel is not (NotificationChannels.InApp or NotificationChannels.Email))
        {
            throw new ValidationException(
                $"Unsupported channel '{channel}'. Use InApp or Email.");
        }

        var key = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"announcement:create:{Guid.NewGuid()}"
            : request.IdempotencyKey.Trim();

        var existing = await _context.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.IdempotencyKey == key, cancellationToken);

        if (existing != null)
        {
            return Map(existing);
        }

        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            AudienceJson = JsonSerializer.Serialize(audience),
            Channel = channel,
            Status = AnnouncementStatus.PendingApproval,
            RequestedByUserId = userId,
            IdempotencyKey = key
        };

        await _context.Announcements.AddAsync(announcement, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            userId,
            "Announcement.Created",
            "Announcement",
            announcement.Id.ToString(),
            $"Channel={channel}; Audience={JsonSerializer.Serialize(audience)}",
            cancellationToken);

        return Map(announcement);
    }

    public async Task<AnnouncementDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var announcement = await _context.Announcements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return announcement == null ? null : Map(announcement);
    }

    public async Task<PagedResult<AnnouncementDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Announcements
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AnnouncementDto>
        {
            Items = items.Select(Map).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AnnouncementDto> ApproveAsync(
        Guid id,
        ApproveAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_currentUser.UserId is not { } approverId)
        {
            throw new ForbiddenException("An authenticated user is required.");
        }

        if (!DecisionRolePolicy.CanApproveAnnouncement(_currentUser.Roles))
        {
            throw new ForbiddenException(
                "Only an EmergencyCoordinator or SystemAdmin may approve announcements.");
        }

        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement == null)
        {
            throw new NotFoundException("Announcement was not found.");
        }

        if (announcement.Status != AnnouncementStatus.PendingApproval)
        {
            throw new ConflictException(
                $"Announcement is already {announcement.Status}; it cannot be approved again.");
        }

        var audience = DeserializeAudience(announcement.AudienceJson);
        var recipients = await ResolveRecipientsAsync(audience, cancellationToken);

        if (recipients.Count == 0)
        {
            // Nothing to fan out — approving an empty audience would be misleading.
            throw new ValidationException("The selected audience contains no active users.");
        }

        // Outbox keys are deterministic. A repeat delivery can never create duplicates.
        var existingKeys = await _context.OutboxMessages
            .AsNoTracking()
            .Where(o => o.IdempotencyKey.StartsWith($"announcement:{id}:"))
            .Select(o => o.IdempotencyKey)
            .ToListAsync(cancellationToken);

        var alreadyQueued = existingKeys.ToHashSet(StringComparer.Ordinal);

        foreach (var recipientId in recipients)
        {
            var deliveryKey = $"announcement:{id}:{recipientId}:{announcement.Channel}";
            if (!alreadyQueued.Add(deliveryKey))
            {
                continue;
            }

            var payload = new NotificationPayloadDto
            {
                RecipientUserId = recipientId,
                TemplateKey = "Announcement.Approved",
                Channel = announcement.Channel,
                Language = "en",
                Data = new Dictionary<string, string>
                {
                    ["title"] = announcement.Title,
                    ["message"] = announcement.Message
                },
                RequestId = id
            };

            await _outbox.EnqueueAsync(
                new OutboxMessage
                {
                    Type = OutboxTypes.NotificationSend,
                    Payload = JsonSerializer.Serialize(payload),
                    EntityId = id,
                    IdempotencyKey = deliveryKey
                },
                cancellationToken);
        }

        var now = _dateTimeService.UtcNow;
        announcement.Status = AnnouncementStatus.Approved;
        announcement.ApprovedByUserId = approverId;
        announcement.ApprovedAt = now;
        announcement.RecipientCount = recipients.Count;
        announcement.UpdatedAt = now;

        // Single SaveChanges persists the approval and all outbox rows atomically.
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            approverId,
            "Announcement.Approved",
            "Announcement",
            id.ToString(),
            $"Recipients={recipients.Count}; Note={request.Note}",
            cancellationToken);

        return Map(announcement);
    }

    public async Task<AnnouncementDto> RejectAsync(
        Guid id,
        RejectAnnouncementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException("A rejection reason is required.");
        }

        if (_currentUser.UserId is not { } userId)
        {
            throw new ForbiddenException("An authenticated user is required.");
        }

        if (!DecisionRolePolicy.CanApproveAnnouncement(_currentUser.Roles))
        {
            throw new ForbiddenException(
                "Only an EmergencyCoordinator or SystemAdmin may reject announcements.");
        }

        var announcement = await _context.Announcements
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (announcement == null)
        {
            throw new NotFoundException("Announcement was not found.");
        }

        if (announcement.Status != AnnouncementStatus.PendingApproval)
        {
            throw new ConflictException(
                $"Announcement is already {announcement.Status}; it cannot be rejected.");
        }

        announcement.Status = AnnouncementStatus.Rejected;
        announcement.RejectionReason = request.Reason.Trim();
        announcement.ApprovedByUserId = userId;
        announcement.ApprovedAt = _dateTimeService.UtcNow;
        announcement.UpdatedAt = announcement.ApprovedAt;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(
            userId,
            "Announcement.Rejected",
            "Announcement",
            id.ToString(),
            announcement.RejectionReason,
            cancellationToken);

        return Map(announcement);
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(
        AnnouncementAudienceDto audience,
        CancellationToken cancellationToken)
    {
        var activeUsers = _context.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Active);

        var selected = new HashSet<Guid>();

        if (audience.AllActiveUsers)
        {
            foreach (var id in await activeUsers.Select(u => u.Id).ToListAsync(cancellationToken))
            {
                selected.Add(id);
            }
        }

        if (audience.UserIds.Count > 0)
        {
            var explicitIds = audience.UserIds.Distinct().ToList();
            foreach (var id in await activeUsers
                         .Where(u => explicitIds.Contains(u.Id))
                         .Select(u => u.Id)
                         .ToListAsync(cancellationToken))
            {
                selected.Add(id);
            }
        }

        if (audience.Roles.Count > 0)
        {
            var roleNames = audience.Roles
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var id in await _context.UserRoles
                         .AsNoTracking()
                         .Where(ur => roleNames.Contains(ur.Role.Name))
                         .Select(ur => ur.UserId)
                         .Distinct()
                         .ToListAsync(cancellationToken))
            {
                if (await activeUsers.AnyAsync(u => u.Id == id, cancellationToken))
                {
                    selected.Add(id);
                }
            }
        }

        return selected.ToList();
    }

    private static void ValidateAudience(AnnouncementAudienceDto audience)
    {
        if (!audience.AllActiveUsers &&
            audience.UserIds.Count == 0 &&
            audience.Roles.Count == 0)
        {
            throw new ValidationException(
                "Select explicit users, roles, or all active users for the audience.");
        }

        var invalidRoles = audience.Roles
            .Where(role => !Roles.All.Contains(role, StringComparer.Ordinal))
            .ToList();

        if (invalidRoles.Count > 0)
        {
            throw new ValidationException(
                $"Invalid audience role(s): {string.Join(", ", invalidRoles)}");
        }
    }

    private static AnnouncementAudienceDto DeserializeAudience(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AnnouncementAudienceDto>(json, JsonOptions)
                   ?? new AnnouncementAudienceDto();
        }
        catch (JsonException)
        {
            return new AnnouncementAudienceDto();
        }
    }

    private static AnnouncementDto Map(Announcement announcement) => new()
    {
        Id = announcement.Id,
        Title = announcement.Title,
        Message = announcement.Message,
        Audience = DeserializeAudience(announcement.AudienceJson),
        Channel = announcement.Channel,
        Status = announcement.Status.ToString(),
        RequestedByUserId = announcement.RequestedByUserId,
        ApprovedByUserId = announcement.ApprovedByUserId,
        ApprovedAt = announcement.ApprovedAt,
        RejectionReason = announcement.RejectionReason,
        DispatchedAt = announcement.DispatchedAt,
        RecipientCount = announcement.RecipientCount,
        CreatedAt = announcement.CreatedAt
    };
}
