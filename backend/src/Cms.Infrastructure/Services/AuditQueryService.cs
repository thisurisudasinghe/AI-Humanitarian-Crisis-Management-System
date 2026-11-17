using Cms.Application.DTOs;
using Cms.Application.DTOs.Audit;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>Read-only audit viewer used by SystemAdmin.</summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private readonly CmsDbContext _db;

    public AuditQueryService(CmsDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<AuditEventDto>> QueryAsync(
        AuditEventQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            filtered = filtered.Where(item => item.Action == action);
        }

        if (query.ActorUserId.HasValue)
        {
            filtered = filtered.Where(item => item.ActorUserId == query.ActorUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            filtered = filtered.Where(item => item.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            var entityId = query.EntityId.Trim();
            filtered = filtered.Where(item => item.EntityId == entityId);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value;
            filtered = filtered.Where(item => item.CreatedAt >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value;
            filtered = filtered.Where(item => item.CreatedAt <= to);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new AuditEventDto
            {
                Id = item.Id,
                Action = item.Action,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                ActorUserId = item.ActorUserId,
                Details = item.Details,
                IpAddress = item.IpAddress,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Details = SensitiveDataMasker.MaskJson(item.Details);
        }

        return new PagedResultDto<AuditEventDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }
}
