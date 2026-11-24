using System.ComponentModel.DataAnnotations;
using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs.Public;
using Cms.Application.Interfaces;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AuditActions = Cms.Application.Common.AuditActions;
using DomainValidationException = Cms.Domain.Exceptions.ValidationException;

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

    public async Task<IReadOnlyList<AlertPublicationDto>> ListAlertsForPublicationAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        return await _context.Set<Alert>()
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertPublicationDto
            {
                Id = a.Id,
                CrisisId = a.CrisisId,
                Title = a.Title,
                Body = a.Body,
                Status = a.Status,
                PublishedAt = a.PublishedAt,
                PublicVisible = a.PublicVisible,
                PublicTitle = a.PublicTitle,
                PublicSummary = a.PublicSummary,
                CrisisType = a.PublicType,
                Severity = a.Severity,
                District = a.District,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                ExpiresAt = a.PublicExpiresAt,
                PublishedByUserId = a.PublishedByUserId
            })
            .ToListAsync(cancellationToken);
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

    public async Task PublishAlertToMapAsync(
        Guid alertId,
        PublishMapAlertDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        ArgumentNullException.ThrowIfNull(request);
        ValidateMapPublication(request);

        var alert = await _context.Set<Alert>()
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken)
            ?? throw new NotFoundException("ALERT_NOT_FOUND", $"Alert {alertId} was not found.");

        var now = _dateTimeService.UtcNow;

        if (!request.Visible)
        {
            // Hiding removes the whole public payload. Private Title/Body stay intact.
            alert.PublicVisible = false;
            alert.PublicTitle = null;
            alert.PublicSummary = null;
            alert.PublicType = null;
            alert.Severity = null;
            alert.District = null;
            alert.Latitude = null;
            alert.Longitude = null;
            alert.PublicExpiresAt = null;
            alert.UpdatedAt = now;

            await _writer.AddAuditAsync(
                AuditActions.AlertHiddenFromMap,
                "Alert",
                alertId,
                new { alertId },
                _currentUser.UserId,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var firstPublished = !alert.PublishedAt.HasValue;
        alert.PublicVisible = true;
        alert.PublicTitle = request.PublicTitle!.Trim();
        alert.PublicSummary = request.PublicSummary!.Trim();
        alert.PublicType = request.CrisisType!.Trim();
        alert.Severity = request.Severity?.Trim();
        alert.District = request.District?.Trim();
        alert.Latitude = request.Latitude!.Value;
        alert.Longitude = request.Longitude!.Value;
        alert.PublicExpiresAt = request.ExpiresAt;
        alert.PublishedByUserId = _currentUser.UserId;
        // Map publication is deliberately independent of the legacy Alert.Status.
        // Changing Status to Published would expose the private Title/Body through
        // the older anonymous /public/alerts feed.
        alert.PublishedAt ??= now;
        alert.UpdatedAt = now;

        await _writer.AddAuditAsync(
            AuditActions.AlertPublishedToMap,
            "Alert",
            alertId,
            new
            {
                alertId,
                publicTitle = alert.PublicTitle,
                type = alert.PublicType,
                severity = alert.Severity,
                district = alert.District,
                expiresAt = alert.PublicExpiresAt,
                firstPublished
            },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicMapDto> GetPublicMapAsync(CancellationToken cancellationToken = default)
    {
        var now = _dateTimeService.UtcNow;

        var incidents = await _context.Set<Alert>()
            .AsNoTracking()
            .Where(a => a.PublicVisible
                        && (a.PublicExpiresAt == null || a.PublicExpiresAt > now))
            .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
            .Select(a => new PublicMapIncidentDto(
                a.Id,
                a.PublicTitle,
                a.PublicSummary,
                a.PublicType,
                a.Severity,
                nameof(AlertStatus.Published),
                a.District,
                a.Latitude,
                a.Longitude,
                a.PublishedAt,
                a.PublicExpiresAt))
            .ToListAsync(cancellationToken);

        var shelters = await _context.Shelters
            .AsNoTracking()
            .Where(s => s.Approved)
            .OrderBy(s => s.Name)
            .Select(s => new PublicMapShelterDto(
                s.Id,
                s.Name,
                s.Lat,
                s.Lng,
                s.Capacity,
                s.Occupancy,
                s.Services))
            .ToListAsync(cancellationToken);

        var aidLocations = await _context.Set<ServiceCentre>()
            .AsNoTracking()
            .Where(s => s.PublicVisible && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new PublicMapAidLocationDto(
                s.Id,
                s.Name,
                s.Address,
                s.Lat,
                s.Lng,
                s.Services,
                s.OpeningHours))
            .ToListAsync(cancellationToken);

        return new PublicMapDto(now, incidents, shelters, aidLocations);
    }

    public async Task SetServiceCentrePublicVisibilityAsync(
        Guid serviceCentreId,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        EnsureCoordinator();
        var centre = await _context.Set<ServiceCentre>()
            .FirstOrDefaultAsync(s => s.Id == serviceCentreId, cancellationToken)
            ?? throw new NotFoundException("SERVICE_CENTRE_NOT_FOUND", $"Service centre {serviceCentreId} was not found.");

        centre.PublicVisible = isPublic;
        centre.UpdatedAt = _dateTimeService.UtcNow;

        await _writer.AddAuditAsync(
            AuditActions.ServiceCentrePublicVisibilityChanged,
            "ServiceCentre",
            serviceCentreId,
            new { serviceCentreId, isPublic },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
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

    private static void ValidateMapPublication(PublishMapAlertDto request)
    {
        var errors = request.Validate(new ValidationContext(request))
            .Where(result => result.MemberNames.Any())
            .GroupBy(result => result.MemberNames.First())
            .ToDictionary(
                group => group.Key,
                group => group.Select(result => result.ErrorMessage ?? "Invalid value.").ToArray());

        if (errors.Count > 0)
        {
            throw new DomainValidationException("MAP_PUBLICATION_INVALID", errors);
        }
    }

    private void EnsureCoordinator()
    {
        if (!_currentUser.Roles.Any(r => CoordinatorRoles.Contains(r)))
        {
            throw new ForbiddenException("COORDINATOR_ROLE_REQUIRED", "EmergencyCoordinator role is required.");
        }
    }
}
