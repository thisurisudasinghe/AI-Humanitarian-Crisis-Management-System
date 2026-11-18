using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public class RequestReader : IRequestReader
{
    private readonly ApplicationDbContext _context;
    private readonly ISensitiveDataProtector _protector;

    public RequestReader(ApplicationDbContext context, ISensitiveDataProtector protector)
    {
        _context = context;
        _protector = protector;
    }

    public async Task<RequestSummaryDto?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var row = await _context.Set<Domain.Entities.Cases.Request>()
            .AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new
            {
                r.Id,
                r.Category,
                r.Urgency,
                r.Status,
                r.Description,
                r.Lat,
                r.Lng,
                r.AreaId,
                r.HouseholdSize,
                r.AccessibilityNeeds,
                r.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new RequestSummaryDto
        {
            Id = row.Id,
            Category = row.Category,
            Urgency = row.Urgency,
            Status = row.Status,
            Description = row.Description,
            Lat = row.Lat,
            Lng = row.Lng,
            AreaId = row.AreaId ?? Guid.Empty,
            HouseholdSize = row.HouseholdSize,
            AccessibilityNeeds = _protector.Unprotect(row.AccessibilityNeeds),
            CreatedAt = row.CreatedAt
        };
    }
}
