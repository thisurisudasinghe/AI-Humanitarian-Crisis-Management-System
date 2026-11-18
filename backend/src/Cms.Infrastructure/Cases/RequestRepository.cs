using System.Linq.Expressions;
using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public class RequestRepository : IRequestRepository
{
    private static readonly RequestStatus[] OpenStatuses =
        Enum.GetValues<RequestStatus>().Where(RequestStateMachine.IsOpen).ToArray();

    private static readonly Expression<Func<Request, int>> UrgencyRank = r =>
        r.Urgency == Urgency.Critical ? 4 :
        r.Urgency == Urgency.High ? 3 :
        r.Urgency == Urgency.Medium ? 2 : 1;

    private readonly ApplicationDbContext _context;
    private readonly IOverduePolicy _overduePolicy;
    private readonly IDateTimeService _dateTimeService;

    public RequestRepository(
        ApplicationDbContext context,
        IOverduePolicy overduePolicy,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _overduePolicy = overduePolicy;
        _dateTimeService = dateTimeService;
    }

    public Task<Request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.Set<Request>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public Task<Request?> GetByIdForUpdateAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _context.Set<Request>().FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public async Task<PagedResult<Request>> ListAsync(
        RequestFilter filter,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeService.UtcNow;
        var cutoff = now.AddHours(-_overduePolicy.SlaHours);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var source = _context.Set<Request>().AsNoTracking().AsQueryable();

        if (filter.RequesterUserId.HasValue)
        {
            source = source.Where(r => r.RequesterUserId == filter.RequesterUserId.Value);
        }

        if (filter.Status.HasValue)
        {
            source = source.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            source = source.Where(r => statuses.Contains(r.Status));
        }

        if (filter.Urgency.HasValue)
        {
            source = source.Where(r => r.Urgency == filter.Urgency.Value);
        }

        if (filter.Category.HasValue)
        {
            source = source.Where(r => r.Category == filter.Category.Value);
        }

        if (filter.AreaId.HasValue)
        {
            source = source.Where(r => r.AreaId == filter.AreaId.Value);
        }

        if (filter.CrisisId.HasValue)
        {
            source = source.Where(r => r.CrisisId == filter.CrisisId.Value);
        }

        if (filter.AssignedTo.HasValue)
        {
            source = source.Where(r => r.AssignedToUserId == filter.AssignedTo.Value);
        }

        if (filter.Overdue == true)
        {
            source = source.Where(r => OpenStatuses.Contains(r.Status) && r.SubmittedAt < cutoff);
        }
        else if (filter.Overdue == false)
        {
            source = source.Where(r => !OpenStatuses.Contains(r.Status) || r.SubmittedAt >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var needle = filter.Q.Trim();
            source = source.Where(r => r.Description.Contains(needle));
        }

        source = query.SortBy?.ToLowerInvariant() switch
        {
            "urgency" => query.SortDescending
                ? source.OrderByDescending(UrgencyRank).ThenByDescending(r => r.SubmittedAt)
                : source.OrderBy(UrgencyRank).ThenBy(r => r.SubmittedAt),
            "age" => query.SortDescending
                ? source.OrderBy(r => r.SubmittedAt)
                : source.OrderByDescending(r => r.SubmittedAt),
            _ => source.OrderByDescending(r => r.SubmittedAt)
        };

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Request>
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task AddAsync(Request request, CancellationToken cancellationToken = default)
    {
        await _context.Set<Request>().AddAsync(request, cancellationToken);
    }
}
