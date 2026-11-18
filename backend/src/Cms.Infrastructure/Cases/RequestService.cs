using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class RequestService : IRequestService
{
    private static readonly HashSet<string> StaffRoles =
        CasesRoles.Staff.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private readonly ApplicationDbContext _context;
    private readonly IRequestRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly IOverduePolicy _overduePolicy;
    private readonly ISensitiveDataProtector _protector;
    private readonly CasesOptions _options;
    private readonly CaseWriteHelper _writer;

    public RequestService(
        ApplicationDbContext context,
        IRequestRepository repository,
        ICurrentUserService currentUser,
        IDateTimeService dateTimeService,
        IOverduePolicy overduePolicy,
        ISensitiveDataProtector protector,
        CasesOptions options,
        CaseWriteHelper writer)
    {
        _context = context;
        _repository = repository;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _overduePolicy = overduePolicy;
        _protector = protector;
        _options = options;
        _writer = writer;
    }

    public async Task<Guid> SubmitAsync(CreateRequestDto request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();

        if (!_options.ApprovedCategories.Contains(request.Category))
        {
            throw new ValidationException(
                "CATEGORY_NOT_APPROVED",
                new Dictionary<string, string[]>
                {
                    [nameof(request.Category)] = new[] { "Category is not an approved service type." }
                });
        }

        Guid? crisisId = request.CrisisId;
        if (request.AreaId.HasValue)
        {
            var area = await _context.Set<Area>()
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.AreaId.Value, cancellationToken)
                ?? throw new ValidationException(
                    "AREA_NOT_FOUND",
                    new Dictionary<string, string[]> { ["AreaId"] = new[] { "Area was not found." } });

            if (crisisId.HasValue && area.CrisisId != crisisId.Value)
            {
                throw new ValidationException(
                    "AREA_CRISIS_MISMATCH",
                    new Dictionary<string, string[]> { ["AreaId"] = new[] { "Area does not belong to the crisis." } });
            }

            crisisId ??= area.CrisisId;
        }

        var now = _dateTimeService.UtcNow;
        var entity = new Request
        {
            Id = Guid.NewGuid(),
            RequesterUserId = userId,
            CrisisId = crisisId,
            AreaId = request.AreaId,
            Category = request.Category,
            Urgency = request.Urgency,
            Description = request.Description,
            Lat = request.Lat,
            Lng = request.Lng,
            HouseholdSize = request.HouseholdSize,
            AccessibilityNeeds = _protector.Protect(request.AccessibilityNeeds),
            Status = RequestStatus.Submitted,
            SubmittedAt = now,
            CreatedAt = now
        };

        await _repository.AddAsync(entity, cancellationToken);

        await _writer.AddHistoryAsync(new RequestStatusHistory
        {
            Id = Guid.NewGuid(),
            RequestId = entity.Id,
            FromStatus = RequestStatus.Submitted,
            ToStatus = RequestStatus.Submitted,
            ChangedByUserId = userId,
            Reason = "Request submitted",
            CreatedAt = now
        }, cancellationToken);

        await _writer.AddAuditAsync(
            "Request.Submitted",
            "Request",
            entity.Id,
            new { category = request.Category.ToString(), urgency = request.Urgency.ToString() },
            userId,
            cancellationToken);

        await _writer.AddOutboxAsync(
            "Request.Submitted",
            entity.Id,
            new { requestId = entity.Id, category = request.Category.ToString(), urgency = request.Urgency.ToString() },
            $"Request.Submitted:{entity.Id}:1",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<PagedResult<RequestListItemDto>> ListMineAsync(
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var result = await _repository.ListAsync(
            new RequestFilter { RequesterUserId = userId },
            query,
            cancellationToken);
        return MapPage(result);
    }

    public async Task<RequestDetailDto?> GetDetailAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await GetVisibleAsync(requestId, cancellationToken);
        var now = _dateTimeService.UtcNow;
        var attachments = await _context.Set<Attachment>()
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AttachmentMetadataDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                ContentType = a.ContentType,
                SizeBytes = a.SizeBytes,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var dto = new RequestDetailDto
        {
            Id = request.Id,
            RequesterUserId = request.RequesterUserId,
            CrisisId = request.CrisisId,
            AreaId = request.AreaId,
            Category = request.Category,
            Urgency = request.Urgency,
            Status = request.Status,
            Description = request.Description,
            Lat = request.Lat,
            Lng = request.Lng,
            HouseholdSize = request.HouseholdSize,
            AccessibilityNeeds = _protector.Unprotect(request.AccessibilityNeeds),
            AssignedToUserId = request.AssignedToUserId,
            MergedIntoRequestId = request.MergedIntoRequestId,
            SubmittedAt = request.SubmittedAt,
            VerifiedAt = request.VerifiedAt,
            TriagedAt = request.TriagedAt,
            ClosedAt = request.ClosedAt,
            SlaAgeHours = _overduePolicy.AgeHours(request.SubmittedAt, now),
            IsOverdue = _overduePolicy.IsOverdue(request.SubmittedAt, now),
            Attachments = attachments,
            Timeline = await GetTimelineCoreAsync(requestId, cancellationToken)
        };
        return dto;
    }

    public async Task UpdateMineAsync(
        Guid requestId,
        UpdateRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetVisibleAsync(requestId, cancellationToken, track: true);

        if (request.AssignedToUserId.HasValue)
        {
            if (!IsStaff())
            {
                throw new ForbiddenException(
                    "STAFF_ROLE_REQUIRED",
                    "Only staff can assign a request.");
            }

            entity.AssignedToUserId = request.AssignedToUserId.Value;
            entity.UpdatedAt = _dateTimeService.UtcNow;
            await _writer.AddAuditAsync(
                "Request.Assigned",
                "Request",
                entity.Id,
                new { assignedToUserId = entity.AssignedToUserId.Value },
                _currentUser.UserId,
                cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        if (entity.RequesterUserId != RequireUserId())
        {
            throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");
        }

        if (entity.Status != RequestStatus.Submitted)
        {
            throw new ConflictException(
                "REQUEST_NOT_EDITABLE",
                $"Request {requestId} can only be edited while Submitted.");
        }

        if (request.Description is not null) entity.Description = request.Description;
        if (request.Lat.HasValue) entity.Lat = request.Lat.Value;
        if (request.Lng.HasValue) entity.Lng = request.Lng.Value;
        if (request.HouseholdSize.HasValue) entity.HouseholdSize = request.HouseholdSize.Value;
        if (request.AccessibilityNeeds is not null)
        {
            entity.AccessibilityNeeds = _protector.Protect(request.AccessibilityNeeds);
        }
        entity.UpdatedAt = _dateTimeService.UtcNow;

        await _writer.AddAuditAsync(
            "Request.Updated",
            "Request",
            entity.Id,
            new { fields = new[] { "description", "location", "householdSize", "accessibilityNeeds" } },
            _currentUser.UserId,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestTimelineEntryDto>> GetTimelineAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        await GetVisibleAsync(requestId, cancellationToken);
        return await GetTimelineCoreAsync(requestId, cancellationToken);
    }

    private async Task<IReadOnlyList<RequestTimelineEntryDto>> GetTimelineCoreAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var history = await _context.Set<RequestStatusHistory>()
            .AsNoTracking()
            .Where(h => h.RequestId == requestId)
            .OrderBy(h => h.CreatedAt)
            .Select(h => new { h.Id, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.Reason, h.CreatedAt })
            .ToListAsync(cancellationToken);

        var actorIds = history
            .Where(h => h.ChangedByUserId.HasValue)
            .Select(h => h.ChangedByUserId!.Value)
            .Distinct()
            .ToList();
        var actors = await _context.Users
            .AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FirstName + " " + u.LastName })
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return history.Select(h => new RequestTimelineEntryDto
        {
            Id = h.Id,
            FromStatus = h.FromStatus,
            ToStatus = h.ToStatus,
            ActorDisplayName = h.ChangedByUserId.HasValue && actors.TryGetValue(h.ChangedByUserId.Value, out var name)
                ? name
                : "System",
            Reason = h.Reason,
            CreatedAt = h.CreatedAt
        }).ToList();
    }

    public async Task<Guid> AddFeedbackAsync(
        Guid requestId,
        CreateFeedbackDto feedback,
        CancellationToken cancellationToken = default)
    {
        var request = await GetVisibleAsync(requestId, cancellationToken);
        var userId = RequireUserId();
        if (request.RequesterUserId != userId)
        {
            throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");
        }

        if (request.Status is not (RequestStatus.Fulfilled or RequestStatus.Closed))
        {
            throw new ConflictException(
                "FEEDBACK_NOT_ALLOWED",
                "Feedback is only available for Fulfilled or Closed requests.");
        }

        var entity = new Feedback
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            SubmittedByUserId = userId,
            Comment = feedback.Comment,
            CreatedAt = _dateTimeService.UtcNow
        };
        await _context.Set<Feedback>().AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            "Request.FeedbackSubmitted",
            "Request",
            requestId,
            new { hasComment = !string.IsNullOrWhiteSpace(feedback.Comment) },
            userId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<PagedResult<RequestListItemDto>> ListStaffAsync(
        RequestFilter filter,
        PagedQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var result = await _repository.ListAsync(filter, query, cancellationToken);
        return MapPage(result);
    }

    public async Task AssignAsync(
        Guid requestId,
        AssignRequestDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();
        var entity = await _context.Set<Request>()
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");

        entity.AssignedToUserId = request.AssignedToUserId;
        entity.UpdatedAt = _dateTimeService.UtcNow;
        await _writer.AddAuditAsync(
            "Request.Assigned",
            "Request",
            requestId,
            new { assignedToUserId = request.AssignedToUserId },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private PagedResult<RequestListItemDto> MapPage(PagedResult<Request> source)
    {
        var now = _dateTimeService.UtcNow;
        return new PagedResult<RequestListItemDto>
        {
            Items = source.Items.Select(r => new RequestListItemDto
            {
                Id = r.Id,
                Category = r.Category,
                Urgency = r.Urgency,
                Status = r.Status,
                Description = r.Description,
                AreaId = r.AreaId,
                SubmittedAt = r.SubmittedAt,
                SlaAgeHours = _overduePolicy.AgeHours(r.SubmittedAt, now),
                IsOverdue = _overduePolicy.IsOverdue(r.SubmittedAt, now)
            }).ToList(),
            Total = source.Total,
            Page = source.Page,
            PageSize = source.PageSize
        };
    }

    private async Task<Request> GetVisibleAsync(
        Guid requestId,
        CancellationToken cancellationToken,
        bool track = false)
    {
        var query = _context.Set<Request>().AsQueryable();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        var request = await query
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken)
            ?? throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");

        if (!IsStaff() && request.RequesterUserId != RequireUserId())
        {
            throw new NotFoundException("REQUEST_NOT_FOUND", $"Request {requestId} was not found.");
        }

        return request;
    }

    private Guid RequireUserId() =>
        _currentUser.UserId
        ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Authenticated user is required.");

    private bool IsStaff() => _currentUser.Roles.Any(r => StaffRoles.Contains(r));

    private void EnsureStaff()
    {
        if (!IsStaff())
        {
            throw new ForbiddenException("STAFF_ROLE_REQUIRED", "Staff role is required.");
        }
    }
}
