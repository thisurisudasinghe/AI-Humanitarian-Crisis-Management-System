using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;
using Cms.Application.Interfaces;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>Read-only dead-letter viewer used by SystemAdmin.</summary>
public sealed class OutboxAdminService : IOutboxAdminService
{
    private readonly CmsDbContext _db;

    public OutboxAdminService(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<OutboxMessageDto>> GetDeadLetteredAsync(
        OutboxQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == OutboxMessageStatus.DeadLettered);

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var type = query.Type.Trim();
            filtered = filtered.Where(message => message.Type == type);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(message => message.UpdatedAt ?? message.CreatedAt)
            .ThenByDescending(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(message => new OutboxMessageDto
            {
                Id = message.Id,
                Type = message.Type,
                EntityId = message.EntityId,
                Status = message.Status,
                RetryCount = message.RetryCount,
                NextRetryAt = message.NextRetryAt,
                ProcessedAt = message.ProcessedAt,
                ErrorMessage = message.ErrorMessage,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<OutboxMessageDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
