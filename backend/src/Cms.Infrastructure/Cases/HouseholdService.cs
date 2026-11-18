using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Cases;

public sealed class HouseholdService : IHouseholdService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeService _dateTimeService;
    private readonly ISensitiveDataProtector _protector;
    private readonly CaseWriteHelper _writer;

    public HouseholdService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
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

    public async Task<HouseholdDto?> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var household = await _context.Set<Household>()
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.UserId == userId, cancellationToken);
        if (household is null)
        {
            return null;
        }

        return new HouseholdDto
        {
            Id = household.Id,
            Size = household.Size,
            AccessibilityNeeds = _protector.Unprotect(household.AccessibilityNeeds),
            ContactPhone = _protector.Unprotect(household.ContactPhone)
        };
    }

    public async Task<Guid> UpsertMineAsync(
        SaveHouseholdDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var now = _dateTimeService.UtcNow;
        var household = await _context.Set<Household>()
            .FirstOrDefaultAsync(h => h.UserId == userId, cancellationToken);

        if (household is null)
        {
            household = new Household
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = now
            };
            await _context.Set<Household>().AddAsync(household, cancellationToken);
        }

        household.Size = request.Size;
        household.AccessibilityNeeds = _protector.Protect(request.AccessibilityNeeds);
        household.ContactPhone = _protector.Protect(request.ContactPhone);
        household.UpdatedAt = now;

        await _writer.AddAuditAsync(
            "Household.Saved",
            "Household",
            household.Id,
            new
            {
                size = request.Size,
                contactPhoneMasked = _protector.MaskPhone(request.ContactPhone),
                accessibilityNeedsMasked = _protector.MaskText(request.AccessibilityNeeds)
            },
            userId,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        return household.Id;
    }

    private Guid RequireUserId() =>
        _currentUser.UserId
        ?? throw new ForbiddenException("AUTHENTICATION_REQUIRED", "Authenticated user is required.");
}
