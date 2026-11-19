using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Cases;

public class RequestReader : IRequestReader
{
    private readonly ApplicationDbContext _context;

    public RequestReader(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RequestSummaryDto?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<Cms.Domain.Entities.Cases.Request>()
            .AsNoTracking()
            .Where(r => r.Id == requestId)
            .Select(r => new RequestSummaryDto
            {
                Id = r.Id,
                Category = r.Category,
                Urgency = r.Urgency,
                Status = r.Status,
                Description = r.Description,
                Lat = r.Household!.Area!.Latitude,
                Lng = r.Household!.Area!.Longitude,
                AreaId = r.Household!.AreaId,
                HouseholdSize = r.Household!.HouseholdSize,
                AccessibilityNeeds = null,
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
