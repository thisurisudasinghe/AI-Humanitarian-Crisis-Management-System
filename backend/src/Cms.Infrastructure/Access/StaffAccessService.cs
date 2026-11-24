using Cms.Application.Access.DTOs;
using Cms.Application.Access.Interfaces;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using AuditActions = Cms.Application.Common.AuditActions;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Entities.Access;
using Cms.Domain.Enums;
using Cms.Domain.Enums.Access;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Access;

public sealed class StaffAccessService : IStaffAccessService
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISensitiveDataProtector _protector;
    private readonly CaseWriteHelper _writer;

    public StaffAccessService(
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

    public async Task<StaffApplicationReceiptDto> SubmitAsync(
        SubmitStaffApplicationDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var applicantUserId = _currentUser.UserId
            ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Sign in before applying for staff access.");

        Validate(request);

        var alreadyPending = await _context.StaffAccessApplications
            .AnyAsync(application =>
                application.ApplicantUserId == applicantUserId &&
                application.Status == StaffApplicationStatus.Pending,
                cancellationToken);

        if (alreadyPending)
        {
            throw new ConflictException(
                "STAFF_APPLICATION_ALREADY_PENDING",
                "An access application is already awaiting review.");
        }

        var now = _dateTimeService.UtcNow;
        var entity = new StaffAccessApplication
        {
            Id = Guid.NewGuid(),
            ApplicantUserId = applicantUserId,
            Reference = AccessKeys.NewReference("SA"),
            FullName = _protector.Protect(request.FullName.Trim()) ?? string.Empty,
            Email = _protector.Protect(request.Email.Trim()) ?? string.Empty,
            Organization = _protector.Protect(request.Organization.Trim()) ?? string.Empty,
            Designation = _protector.Protect(request.Designation.Trim()) ?? string.Empty,
            RequestedRole = request.RequestedRole.Trim(),
            Reason = _protector.Protect(request.Reason.Trim()) ?? string.Empty,
            Status = StaffApplicationStatus.Pending,
            SubmittedAt = now,
            CreatedAt = now
        };

        await _context.StaffAccessApplications.AddAsync(entity, cancellationToken);
        await _writer.AddAuditAsync(
            AuditActions.StaffAccessApplicationSubmitted,
            "StaffAccessApplication",
            entity.Id,
            new { reference = entity.Reference, requestedRole = entity.RequestedRole },
            applicantUserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new StaffApplicationReceiptDto
        {
            Id = entity.Id,
            Reference = entity.Reference,
            Status = entity.Status.ToString(),
            SubmittedAt = entity.SubmittedAt
        };
    }

    public async Task<IReadOnlyList<StaffApplicationDto>> ListMineAsync(
        CancellationToken cancellationToken = default)
    {
        var applicantUserId = _currentUser.UserId
            ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Sign in to view access applications.");

        var applications = await _context.StaffAccessApplications
            .AsNoTracking()
            .Where(application => application.ApplicantUserId == applicantUserId)
            .OrderByDescending(application => application.SubmittedAt)
            .ToListAsync(cancellationToken);

        return applications.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<StaffApplicationDto>> ListForReviewAsync(
        CancellationToken cancellationToken = default)
    {
        var applications = await _context.StaffAccessApplications
            .AsNoTracking()
            .OrderByDescending(application => application.SubmittedAt)
            .ToListAsync(cancellationToken);

        return applications.Select(Map).ToList();
    }

    public async Task<StaffApplicationDto> DecideAsync(
        Guid applicationId,
        ReviewStaffApplicationDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reviewerUserId = _currentUser.UserId
            ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Sign in to review access applications.");

        if (!request.Approved && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ValidationException(
                "REJECTION_REASON_REQUIRED",
                new Dictionary<string, string[]>
                {
                    [nameof(request.Reason)] = new[] { "A reason is required when rejecting an application." }
                });
        }

        var application = await _context.StaffAccessApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == applicationId, cancellationToken)
            ?? throw new NotFoundException("STAFF_APPLICATION_NOT_FOUND", "Application was not found.");

        // Defence in depth: an applicant must never be able to approve or
        // reject their own access request, even if a reviewer role was ever
        // assigned while the application was pending.
        if (application.ApplicantUserId == reviewerUserId)
        {
            throw new ForbiddenException(
                "SELF_APPROVAL_NOT_ALLOWED",
                "You cannot review or decide on your own access application.");
        }

        if (application.Status != StaffApplicationStatus.Pending)
        {
            throw new ConflictException(
                "STAFF_APPLICATION_ALREADY_DECIDED",
                "Only pending applications can be reviewed.");
        }

        var now = _dateTimeService.UtcNow;
        application.Status = request.Approved
            ? StaffApplicationStatus.Approved
            : StaffApplicationStatus.Rejected;
        application.ReviewedAt = now;
        application.ReviewerUserId = reviewerUserId;
        application.RejectionReason = request.Approved ? null : request.Reason!.Trim();
        application.UpdatedAt = now;

        if (request.Approved)
        {
            await GrantRequestedRoleAsync(application, now, reviewerUserId, cancellationToken);
        }

        await _writer.AddAuditAsync(
            request.Approved
                ? AuditActions.StaffAccessApplicationApproved
                : AuditActions.StaffAccessApplicationRejected,
            "StaffAccessApplication",
            application.Id,
            new { reference = application.Reference, requestedRole = application.RequestedRole },
            reviewerUserId,
            cancellationToken);

        var templateKey = request.Approved
            ? "StaffAccess.ApplicationApproved"
            : "StaffAccess.ApplicationRejected";

        await _writer.AddOutboxAsync(
            OutboxEventTypes.NotificationSend,
            application.Id,
            new
            {
                RecipientUserId = application.ApplicantUserId,
                TemplateKey = templateKey,
                Language = "en",
                Data = new Dictionary<string, string>
                {
                    ["reference"] = application.Reference,
                    ["role"] = application.RequestedRole
                }
            },
            OutboxEventTypes.BuildIdempotencyKey(OutboxEventTypes.NotificationSend, application.Id, 2),
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return Map(application);
    }

    private async Task GrantRequestedRoleAsync(
        StaffAccessApplication application,
        DateTime now,
        Guid reviewerUserId,
        CancellationToken cancellationToken)
    {
        if (!StaffAccessRoles.IsRequestable(application.RequestedRole))
        {
            // Defence in depth: a stored application must not be able to grant a
            // privileged role even if historical data predates the allowlist.
            throw new ForbiddenException(
                "ROLE_NOT_GRANTABLE",
                "The requested role cannot be granted through an access application.");
        }

        var applicant = await _context.Users
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(user => user.Id == application.ApplicantUserId, cancellationToken)
            ?? throw new NotFoundException("APPLICANT_NOT_FOUND", "Applicant account was not found.");

        if (applicant.Status != UserStatus.Active)
        {
            throw new ConflictException("APPLICANT_NOT_ACTIVE", "The applicant account is not active.");
        }

        var role = await _context.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Name == application.RequestedRole, cancellationToken)
            ?? throw new NotFoundException("REQUESTED_ROLE_NOT_FOUND", "Requested role is not configured.");

        if (applicant.UserRoles.Any(userRole => userRole.RoleId == role.Id))
        {
            return;
        }

        applicant.UserRoles.Add(new UserRole
        {
            UserId = applicant.Id,
            RoleId = role.Id,
            AssignedAt = now
        });

        await _writer.AddAuditAsync(
            AuditActions.UserRoleChanged,
            "User",
            applicant.Id,
            new { role = role.Name, source = "StaffAccessApplication", reference = application.Reference },
            reviewerUserId,
            cancellationToken);
    }

    private StaffApplicationDto Map(StaffAccessApplication application) => new()
    {
        Id = application.Id,
        Reference = application.Reference,
        FullName = _protector.Unprotect(application.FullName) ?? string.Empty,
        Email = _protector.Unprotect(application.Email) ?? string.Empty,
        Organization = _protector.Unprotect(application.Organization) ?? string.Empty,
        Designation = _protector.Unprotect(application.Designation) ?? string.Empty,
        RequestedRole = application.RequestedRole,
        Status = application.Status.ToString(),
        SubmittedAt = application.SubmittedAt,
        ReviewedAt = application.ReviewedAt,
        RejectionReason = application.RejectionReason
    };

    private static void Validate(SubmitStaffApplicationDto request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FullName)) errors[nameof(request.FullName)] = new[] { "Full name is required." };
        if (string.IsNullOrWhiteSpace(request.Email)) errors[nameof(request.Email)] = new[] { "Email is required." };
        if (string.IsNullOrWhiteSpace(request.Organization)) errors[nameof(request.Organization)] = new[] { "Organization is required." };
        if (string.IsNullOrWhiteSpace(request.Designation)) errors[nameof(request.Designation)] = new[] { "Designation is required." };
        if (!StaffAccessRoles.IsRequestable(request.RequestedRole))
        {
            errors[nameof(request.RequestedRole)] = new[] { "Select a supported staff role." };
        }
        if (string.IsNullOrWhiteSpace(request.Reason)) errors[nameof(request.Reason)] = new[] { "Reason is required." };

        if (errors.Count > 0)
        {
            throw new ValidationException("INVALID_STAFF_APPLICATION", errors);
        }
    }
}
