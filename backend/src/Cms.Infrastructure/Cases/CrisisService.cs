using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class CrisisService : ICrisisService
{
    private static readonly HashSet<string> CoordinatorRoles =
        CasesRoles.CoordinatorOnly.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOverduePolicy _overduePolicy;
    private readonly CaseWriteHelper _writer;

    public CrisisService(
        CmsDbContext context,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService,
        IOverduePolicy overduePolicy,
        CaseWriteHelper writer)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _overduePolicy = overduePolicy;
        _writer = writer;
    }

    public async Task<IReadOnlyList<CrisisDto>> ListCrisesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<Crisis>()
            .AsNoTracking()
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new CrisisDto
            {
                Id = c.Id,
                Name = c.Name,
                Type = c.Type,
                Description = c.Description,
                Status = c.Status,
                StartedAt = c.StartedAt,
                EndedAt = c.EndedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateCrisisAsync(
        CreateCrisisDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var now = _dateTimeService.UtcNow;
        var entity = new Crisis
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Description = request.Description,
            Status = CrisisStatus.Active,
            StartedAt = request.StartedAt,
            CreatedAt = now
        };
        await _context.Set<Crisis>().AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            "Crisis.Created",
            "Crisis",
            entity.Id,
            new { name = request.Name, type = request.Type },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateCrisisAsync(
        Guid crisisId,
        UpdateCrisisDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var entity = await _context.Set<Crisis>()
            .FirstOrDefaultAsync(c => c.Id == crisisId, cancellationToken)
            ?? throw new NotFoundException("CRISIS_NOT_FOUND", $"Crisis {crisisId} was not found.");

        if (request.Name is not null) entity.Name = request.Name;
        if (request.Type is not null) entity.Type = request.Type;
        if (request.Description is not null) entity.Description = request.Description;
        if (request.Status.HasValue) entity.Status = request.Status.Value;
        if (request.EndedAt.HasValue) entity.EndedAt = request.EndedAt.Value;
        if (entity.Status == CrisisStatus.Closed && request.EndedAt is null && entity.EndedAt is null)
        {
            entity.EndedAt = _dateTimeService.UtcNow;
        }
        entity.UpdatedAt = _dateTimeService.UtcNow;

        await _writer.AddAuditAsync(
            "Crisis.Updated",
            "Crisis",
            crisisId,
            new { status = entity.Status.ToString() },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AreaDto>> ListAreasAsync(
        Guid? crisisId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Area>().AsNoTracking().AsQueryable();
        if (crisisId.HasValue)
        {
            query = query.Where(a => a.CrisisId == crisisId.Value);
        }

        return await query
            .OrderBy(a => a.Name)
            .Select(a => new AreaDto
            {
                Id = a.Id,
                CrisisId = a.CrisisId,
                Name = a.Name,
                Description = a.Description,
                Lat = a.Lat,
                Lng = a.Lng,
                RadiusKm = a.RadiusKm
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAreaAsync(
        Guid crisisId,
        CreateAreaDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var exists = await _context.Set<Crisis>().AnyAsync(c => c.Id == crisisId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("CRISIS_NOT_FOUND", $"Crisis {crisisId} was not found.");
        }

        var entity = new Area
        {
            Id = Guid.NewGuid(),
            CrisisId = crisisId,
            Name = request.Name,
            Description = request.Description,
            Lat = request.Lat,
            Lng = request.Lng,
            RadiusKm = request.RadiusKm,
            CreatedAt = _dateTimeService.UtcNow
        };
        await _context.Set<Area>().AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            "Area.Created",
            "Area",
            entity.Id,
            new { crisisId, name = request.Name },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<Guid> CreateAlertAsync(
        Guid crisisId,
        CreateAlertDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        if (!await _context.Set<Crisis>().AnyAsync(c => c.Id == crisisId, cancellationToken))
        {
            throw new NotFoundException("CRISIS_NOT_FOUND", $"Crisis {crisisId} was not found.");
        }

        var now = _dateTimeService.UtcNow;
        var publish = request.Publish;
        var entity = new Alert
        {
            Id = Guid.NewGuid(),
            CrisisId = crisisId,
            Title = request.Title,
            Body = request.Body,
            Status = publish ? AlertStatus.Published : AlertStatus.Draft,
            PublishedAt = publish ? now : null,
            CreatedAt = now
        };
        await _context.Set<Alert>().AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            "Alert.Created",
            "Alert",
            entity.Id,
            new { crisisId, status = entity.Status.ToString() },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAlertAsync(
        Guid alertId,
        UpdateAlertDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var entity = await _context.Set<Alert>()
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken)
            ?? throw new NotFoundException("ALERT_NOT_FOUND", $"Alert {alertId} was not found.");

        if (request.Title is not null) entity.Title = request.Title;
        if (request.Body is not null) entity.Body = request.Body;
        if (request.Publish.HasValue)
        {
            entity.Status = request.Publish.Value ? AlertStatus.Published : AlertStatus.Draft;
            entity.PublishedAt = request.Publish.Value
                ? entity.PublishedAt ?? _dateTimeService.UtcNow
                : null;
        }
        entity.UpdatedAt = _dateTimeService.UtcNow;

        await _writer.AddAuditAsync(
            "Alert.Updated",
            "Alert",
            alertId,
            new { status = entity.Status.ToString() },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertDto>> ListPublicAlertsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<Alert>()
            .AsNoTracking()
            .Where(a => a.Status == AlertStatus.Published)
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Select(a => new AlertDto
            {
                Id = a.Id,
                CrisisId = a.CrisisId,
                Title = a.Title,
                Body = a.Body,
                Status = a.Status,
                PublishedAt = a.PublishedAt,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceCentreDto>> ListPublicServiceCentresAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Set<ServiceCentre>()
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new ServiceCentreDto
            {
                Id = s.Id,
                CrisisId = s.CrisisId,
                Name = s.Name,
                Address = s.Address,
                Lat = s.Lat,
                Lng = s.Lng,
                Services = s.Services,
                OpeningHours = s.OpeningHours
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CrisisSummaryDto> GetSummaryAsync(
        Guid crisisId,
        CancellationToken cancellationToken = default)
    {
        var requests = await _context.Set<Cms.Domain.Entities.Cases.Request>()
            .AsNoTracking()
            .Where(r => r.CrisisId == crisisId)
            .ToListAsync(cancellationToken);
        var areaNames = await _context.Set<Area>()
            .AsNoTracking()
            .Where(a => a.CrisisId == crisisId)
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);
        var now = _dateTimeService.UtcNow;

        return new CrisisSummaryDto
        {
            CrisisId = crisisId,
            ByStatus = requests.GroupBy(r => r.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            ByUrgency = requests.GroupBy(r => r.Urgency.ToString())
                .ToDictionary(g => g.Key, g => g.Count()),
            ByArea = requests.GroupBy(r => r.AreaId)
                .ToDictionary(
                    g => g.Key.HasValue && areaNames.TryGetValue(g.Key.Value, out var name) ? name : "Unassigned",
                    g => g.Count()),
            OpenTotal = requests.Count(r => RequestStateMachine.IsOpen(r.Status)),
            OverdueTotal = requests.Count(r =>
                RequestStateMachine.IsOpen(r.Status) && _overduePolicy.IsOverdue(r.SubmittedAt, now))
        };
    }

    private void EnsureCoordinator()
    {
        if (!_currentUser.Roles.Any(r => CoordinatorRoles.Contains(r)))
        {
            throw new ForbiddenException("COORDINATOR_ROLE_REQUIRED", "EmergencyCoordinator role is required.");
        }
    }
}
