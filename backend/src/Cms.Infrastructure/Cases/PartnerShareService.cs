using Cms.Application.Cases.DTOs;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;

namespace Cms.Infrastructure.Cases;

/// <summary>
/// DEV2 partner visibility seam. Case staff share/revoke; a PartnerOfficer can
/// only enumerate active shares for the organisation in their JWT. Request
/// fields are always obtained from IRequestReader, never from a client hint.
/// FR-PTN-02 accept/reject is deliberately not implemented (M4 dependency).
/// </summary>
public sealed class PartnerShareService
{
    private readonly CmsDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly IRequestReader _requestReader;
    private readonly CaseWriteHelper _writer;

    public PartnerShareService(
        CmsDbContext context,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService,
        IRequestReader requestReader,
        CaseWriteHelper writer)
    {
        _context = context;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
        _requestReader = requestReader;
        _writer = writer;
    }

    public async Task<PartnerShareDto> ShareAsync(
        CreatePartnerShareDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();

        if (request.RequestId == Guid.Empty || request.OrganisationId == Guid.Empty)
        {
            throw new ValidationException(
                "INVALID_PARTNER_SHARE",
                "A request ID and organisation ID are required.");
        }

        var summary = await _requestReader.GetAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException(
                "REQUEST_NOT_FOUND",
                $"Request {request.RequestId} was not found.");

        // The coordinator UI accepts an administrator-supplied organisation ID,
        // so reject typos instead of persisting a share no partner can ever see.
        if (!await _context.Organisations
                .AnyAsync(organisation => organisation.Id == request.OrganisationId, cancellationToken))
        {
            throw new NotFoundException(
                "ORGANISATION_NOT_FOUND",
                $"Organisation {request.OrganisationId} was not found.");
        }

        var duplicate = await _context.Set<PartnerShare>()
            .AnyAsync(
                share => share.RequestId == request.RequestId &&
                         share.OrganisationId == request.OrganisationId &&
                         share.RevokedAt == null,
                cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "DUPLICATE_ACTIVE_SHARE",
                "This request is already actively shared with the organisation.");
        }

        var now = _dateTimeService.UtcNow;
        var share = new PartnerShare
        {
            Id = Guid.NewGuid(),
            RequestId = request.RequestId,
            OrganisationId = request.OrganisationId,
            SharedByUserId = _currentUser.UserId ?? Guid.Empty,
            SharedAt = now,
            CreatedAt = now
        };

        await _context.Set<PartnerShare>().AddAsync(share, cancellationToken);
        await _writer.AddAuditAsync(
            "PartnerShare.Created",
            "PartnerShare",
            share.Id,
            new { requestId = share.RequestId, organisationId = share.OrganisationId },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(share, summary, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerShareDto>> ListAsync(
        Guid? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var isStaff = IsStaff();
        var isPartner = _currentUser.IsInRole(CasesRoles.PartnerOfficer);
        if (!isStaff && !isPartner)
        {
            throw new ForbiddenException(
                "PARTNER_SHARE_FORBIDDEN",
                "Only case staff or partner officers can list partner shares.");
        }

        Guid? organisationId = null;
        if (isPartner)
        {
            organisationId = _currentUser.OrgId
                ?? throw new ForbiddenException(
                    "PARTNER_ORG_REQUIRED",
                    "The current partner account has no organisation scope.");
        }

        var query = _context.Set<PartnerShare>()
            .AsNoTracking()
            .Where(share => share.RevokedAt == null);

        if (organisationId.HasValue)
        {
            query = query.Where(share => share.OrganisationId == organisationId.Value);
        }

        if (requestId.HasValue)
        {
            query = query.Where(share => share.RequestId == requestId.Value);
        }

        var rows = await query
            .OrderByDescending(share => share.SharedAt)
            .ToListAsync(cancellationToken);

        // A partner must not learn whether another organisation's request exists;
        // staff may legitimately see an empty list for a request with no shares.
        if (requestId.HasValue && rows.Count == 0 && !isStaff)
        {
            throw new NotFoundException(
                "PARTNER_SHARE_NOT_FOUND",
                $"Request {requestId} is not shared with your organisation.");
        }

        var result = new List<PartnerShareDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(await ToDtoAsync(row, null, cancellationToken));
        }

        return result;
    }

    public async Task<PartnerShareDto> RevokeAsync(
        Guid shareId,
        CancellationToken cancellationToken = default)
    {
        EnsureStaff();

        var share = await _context.Set<PartnerShare>()
            .FirstOrDefaultAsync(row => row.Id == shareId, cancellationToken)
            ?? throw new NotFoundException(
                "PARTNER_SHARE_NOT_FOUND",
                $"Partner share {shareId} was not found.");

        if (share.RevokedAt.HasValue)
        {
            throw new ConflictException(
                "SHARE_ALREADY_REVOKED",
                "This partner share has already been revoked.");
        }

        var now = _dateTimeService.UtcNow;
        share.RevokedAt = now;
        share.UpdatedAt = now;

        await _writer.AddAuditAsync(
            "PartnerShare.Revoked",
            "PartnerShare",
            share.Id,
            new { requestId = share.RequestId, organisationId = share.OrganisationId },
            _currentUser.UserId,
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return await ToDtoAsync(share, null, cancellationToken);
    }

    private async Task<PartnerShareDto> ToDtoAsync(
        PartnerShare share,
        Cms.Application.DTOs.RequestSummaryDto? knownSummary,
        CancellationToken cancellationToken)
    {
        var summary = knownSummary ?? await _requestReader.GetAsync(share.RequestId, cancellationToken);
        return new PartnerShareDto
        {
            Id = share.Id,
            RequestId = share.RequestId,
            OrganisationId = share.OrganisationId,
            SharedByUserId = share.SharedByUserId,
            SharedAt = share.SharedAt,
            RevokedAt = share.RevokedAt,
            Status = summary?.Status,
            Category = summary?.Category,
            Urgency = summary?.Urgency
        };
    }

    private bool IsStaff() =>
        CasesRoles.Staff.Split(',').Any(role => _currentUser.IsInRole(role));

    private void EnsureStaff()
    {
        if (!IsStaff())
        {
            throw new ForbiddenException(
                "PARTNER_SHARE_FORBIDDEN",
                "Only case staff can share or revoke partner visibility.");
        }
    }
}
