using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities.Access;
using Cms.Domain.Enums.Access;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using AuditActions = Cms.Application.Common.AuditActions;
using DomainValidationException = Cms.Domain.Exceptions.ValidationException;

namespace Cms.Infrastructure.Access;

public sealed class GuestReportService : IGuestReportService
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISensitiveDataProtector _protector;
    private readonly CaseWriteHelper _writer;

    public GuestReportService(
        CmsDbContext context,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService,
        ISensitiveDataProtector protector,
        CaseWriteHelper writer)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _protector = protector;
        _writer = writer;
    }

    public async Task<GuestReportReceiptDto> SubmitAsync(
        CreateGuestReportDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var trackingToken = AccessKeys.NormalizeTrackingToken(request.ClientToken);
        var tokenHash = AccessKeys.HashTrackingToken(trackingToken);

        var existing = await _context.GuestReports
            .AsNoTracking()
            .SingleOrDefaultAsync(report => report.TokenHash == tokenHash, cancellationToken);

        if (existing is not null)
        {
            // Same high-entropy bearer token means a retry of the same submission.
            // Return the original receipt without creating a duplicate report.
            return new GuestReportReceiptDto
            {
                Reference = existing.Reference,
                TrackingToken = trackingToken,
                Status = existing.Status.ToString(),
                SubmittedAt = existing.SubmittedAt
            };
        }

        var now = _dateTimeService.UtcNow;
        var entity = new GuestReport
        {
            Id = Guid.NewGuid(),
            Reference = AccessKeys.NewReference("GR"),
            TokenHash = tokenHash,
            IncidentType = request.IncidentType.Trim(),
            Location = request.Location.Trim(),
            Latitude = request.Latitude.HasValue ? (decimal)request.Latitude.Value : null,
            Longitude = request.Longitude.HasValue ? (decimal)request.Longitude.Value : null,
            Description = request.Description.Trim(),
            ContactInfo = _protector.Protect(string.IsNullOrWhiteSpace(request.ContactInfo)
                ? null
                : request.ContactInfo.Trim()),
            Status = GuestReportStatus.Received,
            SubmittedAt = now,
            UpdatedAt = now,
            CreatedAt = now
        };

        entity.Timeline.Add(new GuestReportTimeline
        {
            Id = Guid.NewGuid(),
            ReportId = entity.Id,
            Status = GuestReportStatus.Received,
            Note = "Report received",
            CreatedAt = now
        });

        await _context.GuestReports.AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            AuditActions.GuestReportSubmitted,
            "GuestReport",
            entity.Id,
            new { reference = entity.Reference, incidentType = entity.IncidentType },
            null,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new GuestReportReceiptDto
        {
            Reference = entity.Reference,
            TrackingToken = trackingToken,
            Status = entity.Status.ToString(),
            SubmittedAt = entity.SubmittedAt
        };
    }

    public async Task<GuestReportTrackingDto> TrackAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!AccessKeys.IsValidTrackingToken(token))
        {
            throw new DomainValidationException(
                "TRACKING_TOKEN_INVALID",
                new Dictionary<string, string[]>
                {
                    ["token"] = new[] { "Enter the tracking code from your confirmation." }
                });
        }

        var normalized = AccessKeys.NormalizeTrackingToken(token);
        var tokenHash = AccessKeys.HashTrackingToken(normalized);
        var report = await _context.GuestReports
            .AsNoTracking()
            .Include(candidate => candidate.Timeline)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken)
            ?? throw new NotFoundException(
                "TRACKING_TOKEN_NOT_FOUND",
                "We could not find a report for that tracking code.");

        await _writer.AddAuditAsync(
            AuditActions.GuestReportTracked,
            "GuestReport",
            report.Id,
            new { reference = report.Reference },
            null,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapTracking(report);
    }

    public async Task<IReadOnlyList<GuestReportSummaryDto>> ListForOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        var reports = await _context.GuestReports
            .AsNoTracking()
            .OrderByDescending(report => report.SubmittedAt)
            .ToListAsync(cancellationToken);

        return reports.Select(report => new GuestReportSummaryDto
        {
            Id = report.Id,
            Reference = report.Reference,
            IncidentType = report.IncidentType,
            Location = report.Location,
            Latitude = report.Latitude.HasValue ? (double)report.Latitude.Value : null,
            Longitude = report.Longitude.HasValue ? (double)report.Longitude.Value : null,
            Description = report.Description,
            ContactInfo = _protector.Unprotect(report.ContactInfo),
            Status = report.Status.ToString(),
            SubmittedAt = report.SubmittedAt,
            UpdatedAt = report.UpdatedAt ?? report.SubmittedAt
        }).ToList();
    }

    public async Task<GuestReportTrackingDto> UpdateStatusAsync(
        Guid reportId,
        UpdateGuestReportStatusDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actorUserId = _currentUser.UserId
            ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Sign in to update guest reports.");

        if (!Enum.TryParse<GuestReportStatus>(request.Status, ignoreCase: true, out var status) ||
            !Enum.IsDefined(status))
        {
            throw new DomainValidationException(
                "INVALID_GUEST_REPORT_STATUS",
                new Dictionary<string, string[]>
                {
                    [nameof(request.Status)] = new[] { "Select a supported report status." }
                });
        }

        var report = await _context.GuestReports
            .Include(candidate => candidate.Timeline)
            .SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken)
            ?? throw new NotFoundException("GUEST_REPORT_NOT_FOUND", "Report was not found.");

        var now = _dateTimeService.UtcNow;
        report.Status = status;
        report.UpdatedAt = now;
        var timelineEntry = new GuestReportTimeline
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            Status = status,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = now
        };

        // The entity uses a client-assigned GUID. Adding it through the DbSet is
        // required so EF marks it Added; relying on navigation change detection
        // would mark it Modified and generate a zero-row UPDATE.
        await _context.GuestReportTimelines.AddAsync(timelineEntry, cancellationToken);
        report.Timeline.Add(timelineEntry);

        await _writer.AddAuditAsync(
            AuditActions.GuestReportStatusChanged,
            "GuestReport",
            report.Id,
            new { reference = report.Reference, status = status.ToString() },
            actorUserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return MapTracking(report);
    }

    private static GuestReportTrackingDto MapTracking(GuestReport report) => new()
    {
        Reference = report.Reference,
        IncidentType = report.IncidentType,
        Location = report.Location,
        Status = report.Status.ToString(),
        SubmittedAt = report.SubmittedAt,
        UpdatedAt = report.UpdatedAt ?? report.SubmittedAt,
        Timeline = report.Timeline
            .OrderBy(entry => entry.CreatedAt)
            .Select(entry => new GuestReportTimelineDto
            {
                Status = entry.Status.ToString(),
                At = entry.CreatedAt
            })
            .ToList()
    };

    private static void Validate(CreateGuestReportDto request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!AccessKeys.IsValidTrackingToken(request.ClientToken))
        {
            errors[nameof(request.ClientToken)] = new[] { "A valid tracking token is required." };
        }
        if (string.IsNullOrWhiteSpace(request.IncidentType))
        {
            errors[nameof(request.IncidentType)] = new[] { "Select an incident type." };
        }
        if (string.IsNullOrWhiteSpace(request.Location))
        {
            errors[nameof(request.Location)] = new[] { "Describe the location." };
        }
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors[nameof(request.Description)] = new[] { "Add a short description." };
        }
        if (request.Latitude.HasValue && (request.Latitude < -90 || request.Latitude > 90))
        {
            errors[nameof(request.Latitude)] = new[] { "Latitude is outside the supported range." };
        }
        if (request.Longitude.HasValue && (request.Longitude < -180 || request.Longitude > 180))
        {
            errors[nameof(request.Longitude)] = new[] { "Longitude is outside the supported range." };
        }
        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            errors[nameof(request.Latitude)] = new[] { "Provide both latitude and longitude, or neither." };
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException("INVALID_GUEST_REPORT", errors);
        }
    }
}
