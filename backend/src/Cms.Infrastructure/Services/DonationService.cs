using System.Globalization;
using System.Linq.Expressions;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Donations;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Lean donation management (M8). Monetary and material donations are recorded
/// manually, allocated to campaigns, tracked against campaign expenditure and
/// reported without exposing donor identities. Suspicious records are flagged
/// by a rule and cleared by a human reviewer; nothing is auto-approved.
/// </summary>
public sealed class DonationService : IDonationService
{
    private const decimal DefaultSuspiciousDonationAmount = 10000m;
    private const string DefaultCurrency = "USD";
    private const int MaxPageSize = 200;

    private static readonly Expression<Func<Donor, DonorDto>> DonorProjection =
        donor => new DonorDto
        {
            Id = donor.Id,
            Name = donor.Name,
            DonorType = donor.DonorType,
            ContactEmail = donor.ContactEmail,
            ContactPhone = donor.ContactPhone,
            IsAnonymous = donor.IsAnonymous,
            IsActive = donor.IsActive,
            CreatedAt = donor.CreatedAt,
            UpdatedAt = donor.UpdatedAt,
            DonationCount = donor.Donations
                .Count(donation => donation.Status != DonationStatus.Cancelled),
            TotalContributed = donor.Donations
                .Where(donation => donation.Status != DonationStatus.Cancelled)
                .Sum(donation => donation.DonationType == DonationType.Monetary
                    ? (donation.Amount ?? 0m)
                    : (donation.EstimatedValue ?? 0m))
        };

    private static readonly Expression<Func<Campaign, CampaignDto>> CampaignProjection =
        campaign => new CampaignDto
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
            GoalAmount = campaign.GoalAmount,
            Currency = campaign.Currency,
            StartsAt = campaign.StartsAt,
            EndsAt = campaign.EndsAt,
            Status = campaign.Status,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            RaisedAmount = campaign.Allocations.Sum(allocation => (decimal?)allocation.Amount) ?? 0m,
            SpentAmount = campaign.Expenses.Sum(expense => (decimal?)expense.Amount) ?? 0m,
            DonationCount = campaign.Allocations
                .Select(allocation => allocation.DonationId)
                .Distinct()
                .Count()
        };

    private static readonly Expression<Func<Donation, DonationDto>> DonationProjection =
        donation => new DonationDto
        {
            Id = donation.Id,
            Reference = donation.Reference,
            DonorId = donation.DonorId,
            DonorName = donation.Donor.Name,
            DonationType = donation.DonationType,
            Amount = donation.Amount,
            Currency = donation.Currency,
            ItemDescription = donation.ItemDescription,
            Quantity = donation.Quantity,
            EstimatedValue = donation.EstimatedValue,
            ReceivedAt = donation.ReceivedAt,
            Status = donation.Status,
            IsSuspicious = donation.IsSuspicious,
            SuspiciousReason = donation.SuspiciousReason,
            Notes = donation.Notes,
            CreatedAt = donation.CreatedAt,
            UpdatedAt = donation.UpdatedAt,
            AllocatedAmount = donation.Allocations
                .Sum(allocation => (decimal?)allocation.Amount) ?? 0m,
            Allocations = donation.Allocations
                .OrderBy(allocation => allocation.CreatedAt)
                .Select(allocation => new DonationAllocationDto
                {
                    Id = allocation.Id,
                    DonationId = allocation.DonationId,
                    CampaignId = allocation.CampaignId,
                    CampaignName = allocation.Campaign.Name,
                    Amount = allocation.Amount,
                    Note = allocation.Note,
                    IsSuspicious = allocation.IsSuspicious,
                    SuspiciousReason = allocation.SuspiciousReason,
                    CreatedAt = allocation.CreatedAt
                })
                .ToList()
        };

    private readonly CmsDbContext _db;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeService _dateTimeService;

    public DonationService(
        CmsDbContext db,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService)
    {
        _db = db;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _dateTimeService = dateTimeService;
    }

    // -----------------------------------------------------------------------
    // Donors
    // -----------------------------------------------------------------------

    public async Task<PagedResultDto<DonorDto>> GetDonorsAsync(
        DonorQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var donors = _db.Donors.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            donors = donors.Where(donor =>
                donor.Name.ToLower().Contains(term) ||
                (donor.ContactEmail != null &&
                 donor.ContactEmail.ToLower().Contains(term)));
        }

        if (query.IsActive.HasValue)
        {
            var isActive = query.IsActive.Value;
            donors = donors.Where(donor => donor.IsActive == isActive);
        }

        var totalCount = await donors.CountAsync(cancellationToken);

        var items = await donors
            .OrderBy(donor => donor.Name)
            .ThenBy(donor => donor.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(DonorProjection)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<DonorDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<DonorDto> GetDonorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _db.Donors
            .AsNoTracking()
            .Where(donor => donor.Id == id)
            .Select(DonorProjection)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Donor was not found.", "DONOR_NOT_FOUND");
    }

    public async Task<DonorDto> CreateDonorAsync(
        CreateDonorRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ValidationException("Donor name is required.");
        }

        var donor = new Donor
        {
            Name = name,
            DonorType = request.DonorType,
            ContactEmail = NormalizeOptional(request.ContactEmail),
            ContactPhone = NormalizeOptional(request.ContactPhone),
            IsAnonymous = request.IsAnonymous,
            IsActive = true,
            CreatedByUserId = _currentUser.UserId
        };

        _db.Donors.Add(donor);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonorCreated,
            "don.Donor",
            donor.Id.ToString(),
            new
            {
                donor.Name,
                donorType = donor.DonorType.ToString(),
                donor.IsAnonymous
            },
            cancellationToken);

        return await GetDonorAsync(donor.Id, cancellationToken);
    }

    public async Task<DonorDto> UpdateDonorAsync(
        Guid id,
        UpdateDonorRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var donor = await _db.Donors
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("Donor was not found.", "DONOR_NOT_FOUND");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ValidationException("Donor name cannot be empty.");
            }

            donor.Name = name;
        }

        if (request.DonorType.HasValue)
        {
            donor.DonorType = request.DonorType.Value;
        }

        if (request.ContactEmail is not null)
        {
            donor.ContactEmail = NormalizeOptional(request.ContactEmail);
        }

        if (request.ContactPhone is not null)
        {
            donor.ContactPhone = NormalizeOptional(request.ContactPhone);
        }

        if (request.IsAnonymous.HasValue)
        {
            donor.IsAnonymous = request.IsAnonymous.Value;
        }

        if (request.IsActive.HasValue)
        {
            donor.IsActive = request.IsActive.Value;
        }

        donor.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonorUpdated,
            "don.Donor",
            donor.Id.ToString(),
            new { donor.Name, donor.IsActive, donor.IsAnonymous },
            cancellationToken);

        return await GetDonorAsync(donor.Id, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Campaigns
    // -----------------------------------------------------------------------

    public async Task<PagedResultDto<CampaignDto>> GetCampaignsAsync(
        CampaignQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var campaigns = _db.Campaigns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            campaigns = campaigns.Where(campaign =>
                campaign.Name.ToLower().Contains(term) ||
                (campaign.Description != null &&
                 campaign.Description.ToLower().Contains(term)));
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            campaigns = campaigns.Where(campaign => campaign.Status == status);
        }

        var totalCount = await campaigns.CountAsync(cancellationToken);

        var items = await campaigns
            .OrderByDescending(campaign => campaign.StartsAt)
            .ThenBy(campaign => campaign.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(CampaignProjection)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            CompleteCampaignDto(item);
        }

        return new PagedResultDto<CampaignDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CampaignDto> GetCampaignAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(CampaignProjection)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Campaign was not found.", "CAMPAIGN_NOT_FOUND");

        CompleteCampaignDto(campaign);
        return campaign;
    }

    public async Task<CampaignDto> CreateCampaignAsync(
        CreateCampaignRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ValidationException("Campaign name is required.");
        }

        var currency = NormalizeCurrency(request.Currency);
        var startsAt = request.StartsAt ?? _dateTimeService.UtcNow;

        if (request.EndsAt.HasValue && request.EndsAt.Value < startsAt)
        {
            throw new ValidationException(
                "The campaign end date cannot be before its start date.");
        }

        var campaign = new Campaign
        {
            Name = name,
            Description = NormalizeOptional(request.Description),
            GoalAmount = Round(request.GoalAmount),
            Currency = currency,
            StartsAt = startsAt,
            EndsAt = request.EndsAt,
            Status = CampaignStatus.Draft,
            CreatedByUserId = _currentUser.UserId
        };

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.CampaignCreated,
            "don.Campaign",
            campaign.Id.ToString(),
            new
            {
                campaign.Name,
                campaign.GoalAmount,
                campaign.Currency,
                status = campaign.Status.ToString()
            },
            cancellationToken);

        return await GetCampaignAsync(campaign.Id, cancellationToken);
    }

    public async Task<CampaignDto> UpdateCampaignAsync(
        Guid id,
        UpdateCampaignRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var campaign = await _db.Campaigns
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("Campaign was not found.", "CAMPAIGN_NOT_FOUND");

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ValidationException("Campaign name cannot be empty.");
            }

            campaign.Name = name;
        }

        if (request.Description is not null)
        {
            campaign.Description = NormalizeOptional(request.Description);
        }

        if (request.GoalAmount.HasValue)
        {
            campaign.GoalAmount = Round(request.GoalAmount.Value);
        }

        if (request.EndsAt.HasValue)
        {
            if (request.EndsAt.Value < campaign.StartsAt)
            {
                throw new ValidationException(
                    "The campaign end date cannot be before its start date.");
            }

            campaign.EndsAt = request.EndsAt;
        }

        if (request.Status.HasValue)
        {
            campaign.Status = ApplyStatusTransition(campaign.Status, request.Status.Value);
        }

        campaign.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.CampaignUpdated,
            "don.Campaign",
            campaign.Id.ToString(),
            new
            {
                campaign.Name,
                campaign.GoalAmount,
                status = campaign.Status.ToString()
            },
            cancellationToken);

        return await GetCampaignAsync(campaign.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CampaignExpenseDto>> GetCampaignExpensesAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCampaignExistsAsync(campaignId, cancellationToken);

        return await _db.CampaignExpenses
            .AsNoTracking()
            .Where(expense => expense.CampaignId == campaignId)
            .OrderByDescending(expense => expense.SpentAt)
            .ThenByDescending(expense => expense.Id)
            .Select(expense => new CampaignExpenseDto
            {
                Id = expense.Id,
                CampaignId = expense.CampaignId,
                Description = expense.Description,
                Amount = expense.Amount,
                Currency = expense.Currency,
                SpentAt = expense.SpentAt,
                CreatedAt = expense.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CampaignExpenseDto> CreateCampaignExpenseAsync(
        Guid campaignId,
        CreateCampaignExpenseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var campaign = await _db.Campaigns
            .SingleOrDefaultAsync(candidate => candidate.Id == campaignId, cancellationToken)
            ?? throw new NotFoundException("Campaign was not found.", "CAMPAIGN_NOT_FOUND");

        if (campaign.Status == CampaignStatus.Cancelled)
        {
            throw new DomainException(
                "Expenses cannot be recorded against a cancelled campaign.",
                "CAMPAIGN_CANCELLED");
        }

        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            throw new ValidationException("Expense description is required.");
        }

        var currency = request.Currency is null
            ? campaign.Currency
            : NormalizeCurrency(request.Currency);

        if (!string.Equals(currency, campaign.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"The expense currency must match the campaign currency ({campaign.Currency}).");
        }

        var now = _dateTimeService.UtcNow;
        var spentAt = request.SpentAt ?? now;

        if (spentAt > now.AddMinutes(5))
        {
            throw new ValidationException("The expense date cannot be in the future.");
        }

        var expense = new CampaignExpense
        {
            CampaignId = campaign.Id,
            Description = description,
            Amount = Round(request.Amount),
            Currency = currency,
            SpentAt = spentAt,
            RecordedByUserId = _currentUser.UserId
        };

        _db.CampaignExpenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.CampaignExpenseRecorded,
            "don.CampaignExpense",
            expense.Id.ToString(),
            new
            {
                campaignId = campaign.Id,
                expense.Description,
                expense.Amount,
                expense.Currency
            },
            cancellationToken);

        return new CampaignExpenseDto
        {
            Id = expense.Id,
            CampaignId = expense.CampaignId,
            Description = expense.Description,
            Amount = expense.Amount,
            Currency = expense.Currency,
            SpentAt = expense.SpentAt,
            CreatedAt = expense.CreatedAt
        };
    }

    // -----------------------------------------------------------------------
    // Public donor portal (caller-scoped views)
    // -----------------------------------------------------------------------

    public async Task<DonorDto?> GetDonorForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var donor = await _db.Donors
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        return donor is null ? null : await GetDonorAsync(donor.Id, cancellationToken);
    }

    public async Task<PagedResultDto<DonationDto>> GetDonationsForUserAsync(
        Guid userId,
        DonationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var donor = await _db.Donors
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (donor is null)
        {
            return new PagedResultDto<DonationDto>
            {
                Page = Math.Max(1, query.Page),
                PageSize = Math.Clamp(query.PageSize, 1, MaxPageSize)
            };
        }

        // The caller's donor id is always enforced server-side. Any donor id
        // supplied by the client is ignored so portal users cannot read or
        // write another donor's contribution history.
        query.DonorId = donor.Id;
        return await GetDonationsAsync(query, cancellationToken);
    }

    public async Task<DonationDto> CreateDonationForUserAsync(
        Guid userId,
        CreateDonationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var donor = await _db.Donors
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(
                "A donor profile is not linked to this account.",
                "DONOR_PROFILE_NOT_FOUND");

        request.DonorId = donor.Id;
        return await CreateDonationAsync(request, cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Donations
    // -----------------------------------------------------------------------

    public async Task<PagedResultDto<DonationDto>> GetDonationsAsync(
        DonationQueryDto query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        var donations = _db.Donations.AsNoTracking().AsQueryable();

        if (query.DonorId.HasValue)
        {
            var donorId = query.DonorId.Value;
            donations = donations.Where(donation => donation.DonorId == donorId);
        }

        if (query.CampaignId.HasValue)
        {
            var campaignId = query.CampaignId.Value;
            donations = donations.Where(donation =>
                donation.Allocations.Any(allocation => allocation.CampaignId == campaignId));
        }

        if (query.DonationType.HasValue)
        {
            var donationType = query.DonationType.Value;
            donations = donations.Where(donation => donation.DonationType == donationType);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            donations = donations.Where(donation => donation.Status == status);
        }

        if (query.IsSuspicious.HasValue)
        {
            var isSuspicious = query.IsSuspicious.Value;
            donations = donations.Where(donation => donation.IsSuspicious == isSuspicious);
        }

        if (query.From.HasValue)
        {
            var from = query.From.Value;
            donations = donations.Where(donation => donation.ReceivedAt >= from);
        }

        if (query.To.HasValue)
        {
            var to = query.To.Value;
            donations = donations.Where(donation => donation.ReceivedAt <= to);
        }

        var totalCount = await donations.CountAsync(cancellationToken);

        var items = await donations
            .OrderByDescending(donation => donation.ReceivedAt)
            .ThenByDescending(donation => donation.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(DonationProjection)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            CompleteDonationDto(item);
        }

        return new PagedResultDto<DonationDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<DonationDto> GetDonationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var donation = await _db.Donations
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(DonationProjection)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Donation was not found.", "DONATION_NOT_FOUND");

        CompleteDonationDto(donation);
        return donation;
    }

    public async Task<DonationDto> CreateDonationAsync(
        CreateDonationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var donor = await _db.Donors
            .SingleOrDefaultAsync(candidate => candidate.Id == request.DonorId, cancellationToken)
            ?? throw new NotFoundException("Donor was not found.", "DONOR_NOT_FOUND");

        if (!donor.IsActive)
        {
            throw new DomainException(
                "Inactive donors cannot receive new donations.",
                "DONOR_INACTIVE");
        }

        var errors = new Dictionary<string, string[]>();
        var currency = NormalizeCurrency(request.Currency);
        var itemDescription = NormalizeOptional(request.ItemDescription);

        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            errors["currency"] = ["Currency must be a three-letter code."];
        }

        if (request.DonationType == DonationType.Monetary)
        {
            if (request.Amount is null or <= 0)
            {
                errors["amount"] =
                    ["A monetary donation requires an amount greater than zero."];
            }

            if (itemDescription is not null)
            {
                errors["itemDescription"] =
                    ["A monetary donation cannot carry an item description."];
            }

            if (request.Quantity.HasValue)
            {
                errors["quantity"] = ["A monetary donation cannot carry a quantity."];
            }

            if (request.EstimatedValue.HasValue)
            {
                errors["estimatedValue"] =
                    ["A monetary donation cannot carry an estimated value."];
            }
        }
        else
        {
            if (request.Amount.HasValue)
            {
                errors["amount"] = ["A material donation cannot carry a cash amount."];
            }

            if (itemDescription is null)
            {
                errors["itemDescription"] =
                    ["A material donation requires an item description."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("The donation is not valid.", errors);
        }

        var now = _dateTimeService.UtcNow;
        var receivedAt = request.ReceivedAt ?? now;

        if (receivedAt > now.AddMinutes(5))
        {
            throw new ValidationException("The donation date cannot be in the future.");
        }

        var value = request.DonationType == DonationType.Monetary
            ? request.Amount
            : request.EstimatedValue;

        var threshold = await GetSuspiciousThresholdAsync(cancellationToken);
        var isSuspicious = value.HasValue && value.Value > threshold;

        var donation = new Donation
        {
            Reference = BuildReference(now),
            DonorId = donor.Id,
            DonationType = request.DonationType,
            Amount = request.Amount.HasValue ? Round(request.Amount.Value) : null,
            Currency = currency,
            ItemDescription = itemDescription,
            Quantity = request.Quantity,
            EstimatedValue = request.EstimatedValue.HasValue
                ? Round(request.EstimatedValue.Value)
                : null,
            ReceivedAt = receivedAt,
            Status = DonationStatus.Received,
            IsSuspicious = isSuspicious,
            SuspiciousReason = isSuspicious
                ? $"Value exceeds the configured review threshold of {threshold:0.##}."
                : null,
            Notes = NormalizeOptional(request.Notes),
            RecordedByUserId = _currentUser.UserId
        };

        _db.Donations.Add(donation);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonationRecorded,
            "don.Donation",
            donation.Id.ToString(),
            new
            {
                donation.Reference,
                donorId = donor.Id,
                donationType = donation.DonationType.ToString(),
                donation.Amount,
                donation.EstimatedValue,
                donation.Currency,
                isSuspicious
            },
            cancellationToken);

        if (isSuspicious)
        {
            await _auditLogger.LogAsync(
                AuditActions.DonationFlagged,
                "don.Donation",
                donation.Id.ToString(),
                new { reason = donation.SuspiciousReason, automated = true },
                cancellationToken);
        }

        return await GetDonationAsync(donation.Id, cancellationToken);
    }

    public async Task<DonationDto> AllocateDonationAsync(
        Guid donationId,
        CreateDonationAllocationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var donation = await _db.Donations
            .Include(candidate => candidate.Allocations)
            .SingleOrDefaultAsync(candidate => candidate.Id == donationId, cancellationToken)
            ?? throw new NotFoundException("Donation was not found.", "DONATION_NOT_FOUND");

        if (donation.Status == DonationStatus.Cancelled)
        {
            throw new DomainException(
                "Cancelled donations cannot be allocated.",
                "DONATION_CANCELLED");
        }

        var allocatable = donation.DonationType == DonationType.Monetary
            ? donation.Amount
            : donation.EstimatedValue;

        if (allocatable is null)
        {
            throw new DomainException(
                "This donation has no recorded value to allocate.",
                "DONATION_NOT_VALUED");
        }

        var campaign = await _db.Campaigns
            .SingleOrDefaultAsync(candidate => candidate.Id == request.CampaignId, cancellationToken)
            ?? throw new NotFoundException("Campaign was not found.", "CAMPAIGN_NOT_FOUND");

        if (campaign.Status != CampaignStatus.Active)
        {
            throw new DomainException(
                "Donations can only be allocated to an active campaign.",
                "CAMPAIGN_NOT_ACTIVE");
        }

        if (!string.Equals(campaign.Currency, donation.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(
                $"Campaign currency {campaign.Currency} does not match donation currency {donation.Currency}.",
                "CURRENCY_MISMATCH");
        }

        var alreadyAllocated = donation.Allocations.Sum(allocation => allocation.Amount);
        var amount = Round(request.Amount);

        if (alreadyAllocated + amount > allocatable.Value)
        {
            throw new DomainException(
                $"Allocation exceeds the remaining allocatable value of {allocatable.Value - alreadyAllocated:0.##}.",
                "ALLOCATION_EXCEEDS_VALUE");
        }

        var campaignRaised = await _db.DonationAllocations
            .Where(allocation => allocation.CampaignId == campaign.Id)
            .SumAsync(allocation => (decimal?)allocation.Amount, cancellationToken) ?? 0m;

        var overGoal = campaignRaised + amount > campaign.GoalAmount;

        var allocation = new DonationAllocation
        {
            DonationId = donation.Id,
            CampaignId = campaign.Id,
            Amount = amount,
            Note = NormalizeOptional(request.Note),
            IsSuspicious = overGoal,
            SuspiciousReason = overGoal
                ? $"Allocation pushes campaign funding above its goal of {campaign.GoalAmount:0.##} {campaign.Currency}."
                : null,
            AllocatedByUserId = _currentUser.UserId
        };

        _db.DonationAllocations.Add(allocation);

        if (alreadyAllocated + amount >= allocatable.Value)
        {
            donation.Status = DonationStatus.Allocated;
        }

        donation.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonationAllocated,
            "don.DonationAllocation",
            allocation.Id.ToString(),
            new
            {
                donationId = donation.Id,
                campaignId = campaign.Id,
                allocation.Amount,
                isSuspicious = overGoal
            },
            cancellationToken);

        if (overGoal)
        {
            await _auditLogger.LogAsync(
                AuditActions.DonationAllocationFlagged,
                "don.DonationAllocation",
                allocation.Id.ToString(),
                new { reason = allocation.SuspiciousReason, automated = true },
                cancellationToken);
        }

        return await GetDonationAsync(donation.Id, cancellationToken);
    }

    public async Task<DonationDto> SetDonationFlagAsync(
        Guid donationId,
        FlagRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var donation = await _db.Donations
            .SingleOrDefaultAsync(candidate => candidate.Id == donationId, cancellationToken)
            ?? throw new NotFoundException("Donation was not found.", "DONATION_NOT_FOUND");

        donation.IsSuspicious = request.IsSuspicious;
        donation.SuspiciousReason = request.IsSuspicious
            ? NormalizeOptional(request.Reason) ?? "Flagged for review by a Donor Officer."
            : null;
        donation.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonationFlagged,
            "don.Donation",
            donation.Id.ToString(),
            new
            {
                isSuspicious = donation.IsSuspicious,
                reason = donation.SuspiciousReason,
                reviewed = true
            },
            cancellationToken);

        return await GetDonationAsync(donation.Id, cancellationToken);
    }

    public async Task<DonationAllocationDto> SetAllocationFlagAsync(
        Guid allocationId,
        FlagRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allocation = await _db.DonationAllocations
            .Include(candidate => candidate.Campaign)
            .SingleOrDefaultAsync(candidate => candidate.Id == allocationId, cancellationToken)
            ?? throw new NotFoundException(
                "Donation allocation was not found.",
                "ALLOCATION_NOT_FOUND");

        allocation.IsSuspicious = request.IsSuspicious;
        allocation.SuspiciousReason = request.IsSuspicious
            ? NormalizeOptional(request.Reason) ?? "Flagged for review by a Donor Officer."
            : null;
        allocation.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.DonationAllocationFlagged,
            "don.DonationAllocation",
            allocation.Id.ToString(),
            new
            {
                isSuspicious = allocation.IsSuspicious,
                reason = allocation.SuspiciousReason,
                reviewed = true
            },
            cancellationToken);

        return new DonationAllocationDto
        {
            Id = allocation.Id,
            DonationId = allocation.DonationId,
            CampaignId = allocation.CampaignId,
            CampaignName = allocation.Campaign.Name,
            Amount = allocation.Amount,
            Note = allocation.Note,
            IsSuspicious = allocation.IsSuspicious,
            SuspiciousReason = allocation.SuspiciousReason,
            CreatedAt = allocation.CreatedAt
        };
    }

    // -----------------------------------------------------------------------
    // Transparency report
    // -----------------------------------------------------------------------

    public async Task<DonationReportDto> GetReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ValidationException(
                "The end of the reporting period must be on or after the start.");
        }

        var donationRows = await _db.Donations
            .AsNoTracking()
            .Where(donation => donation.ReceivedAt >= from && donation.ReceivedAt <= to)
            .Select(donation => new
            {
                donation.Id,
                donation.DonorId,
                donation.DonationType,
                donation.Amount,
                donation.EstimatedValue,
                donation.Currency,
                donation.IsSuspicious,
                donation.Status
            })
            .ToListAsync(cancellationToken);

        var allocationRows = await _db.DonationAllocations
            .AsNoTracking()
            .Where(allocation =>
                allocation.Donation.ReceivedAt >= from &&
                allocation.Donation.ReceivedAt <= to)
            .Select(allocation => new
            {
                allocation.Id,
                allocation.CampaignId,
                allocation.Amount,
                allocation.IsSuspicious,
                Currency = allocation.Donation.Currency,
                Status = allocation.Donation.Status
            })
            .ToListAsync(cancellationToken);

        var expenseRows = await _db.CampaignExpenses
            .AsNoTracking()
            .Where(expense => expense.SpentAt >= from && expense.SpentAt <= to)
            .Select(expense => new
            {
                expense.CampaignId,
                expense.Amount,
                expense.Currency
            })
            .ToListAsync(cancellationToken);

        var campaignRows = await _db.Campaigns
            .AsNoTracking()
            .Select(campaign => new
            {
                campaign.Id,
                campaign.Name,
                campaign.Status,
                campaign.Currency,
                campaign.GoalAmount
            })
            .ToListAsync(cancellationToken);

        var activeDonations = donationRows
            .Where(donation => donation.Status != DonationStatus.Cancelled)
            .ToList();

        var activeAllocations = allocationRows
            .Where(allocation => allocation.Status != DonationStatus.Cancelled)
            .ToList();

        var currencies = activeDonations
            .Select(donation => donation.Currency)
            .Concat(activeAllocations.Select(allocation => allocation.Currency))
            .Concat(expenseRows.Select(expense => expense.Currency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(currency => currency, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var currencyTotals = currencies
            .Select(currency =>
            {
                var donations = activeDonations
                    .Where(donation => string.Equals(
                        donation.Currency, currency, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                return new DonationCurrencyTotalDto
                {
                    Currency = currency,
                    MonetaryTotal = Round(donations
                        .Where(donation => donation.DonationType == DonationType.Monetary)
                        .Sum(donation => donation.Amount ?? 0m)),
                    MaterialEstimatedTotal = Round(donations
                        .Where(donation => donation.DonationType == DonationType.Material)
                        .Sum(donation => donation.EstimatedValue ?? 0m)),
                    AllocatedTotal = Round(activeAllocations
                        .Where(allocation => string.Equals(
                            allocation.Currency, currency, StringComparison.OrdinalIgnoreCase))
                        .Sum(allocation => allocation.Amount)),
                    SpentTotal = Round(expenseRows
                        .Where(expense => string.Equals(
                            expense.Currency, currency, StringComparison.OrdinalIgnoreCase))
                        .Sum(expense => expense.Amount)),
                    DonationCount = donations.Count,
                    DonorCount = donations
                        .Select(donation => donation.DonorId)
                        .Distinct()
                        .Count(),
                    SuspiciousCount = donations.Count(donation => donation.IsSuspicious)
                };
            })
            .ToArray();

        var reportedCampaignIds = activeAllocations
            .Select(allocation => allocation.CampaignId)
            .Concat(expenseRows.Select(expense => expense.CampaignId))
            .ToHashSet();

        var campaignLines = campaignRows
            .Where(campaign => reportedCampaignIds.Contains(campaign.Id))
            .Select(campaign =>
            {
                var raised = Round(activeAllocations
                    .Where(allocation => allocation.CampaignId == campaign.Id)
                    .Sum(allocation => allocation.Amount));

                var spent = Round(expenseRows
                    .Where(expense => expense.CampaignId == campaign.Id)
                    .Sum(expense => expense.Amount));

                return new CampaignReportLineDto
                {
                    CampaignId = campaign.Id,
                    Name = campaign.Name,
                    Status = campaign.Status,
                    Currency = campaign.Currency,
                    GoalAmount = Round(campaign.GoalAmount),
                    RaisedAmount = raised,
                    SpentAmount = spent,
                    RemainingAmount = Round(raised - spent),
                    ProgressPercent = Percent(raised, campaign.GoalAmount)
                };
            })
            .OrderBy(campaign => campaign.Name)
            .ToArray();

        return new DonationReportDto
        {
            From = from,
            To = to,
            GeneratedAt = _dateTimeService.UtcNow,
            TotalDonationCount = activeDonations.Count,
            TotalDonorCount = activeDonations
                .Select(donation => donation.DonorId)
                .Distinct()
                .Count(),
            SuspiciousDonationCount = activeDonations
                .Count(donation => donation.IsSuspicious),
            SuspiciousAllocationCount = activeAllocations
                .Count(allocation => allocation.IsSuspicious),
            CurrencyTotals = currencyTotals,
            Campaigns = campaignLines
        };
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task EnsureCampaignExistsAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        if (!await _db.Campaigns.AnyAsync(
                campaign => campaign.Id == campaignId,
                cancellationToken))
        {
            throw new NotFoundException("Campaign was not found.", "CAMPAIGN_NOT_FOUND");
        }
    }

    private async Task<decimal> GetSuspiciousThresholdAsync(
        CancellationToken cancellationToken)
    {
        var value = await _db.SystemSettings
            .AsNoTracking()
            .Where(setting => setting.Key == SystemSettingKeys.SuspiciousDonationAmount)
            .Select(setting => setting.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed > 0
            ? parsed
            : DefaultSuspiciousDonationAmount;
    }

    private static CampaignStatus ApplyStatusTransition(
        CampaignStatus current,
        CampaignStatus requested)
    {
        if (current == requested)
        {
            return current;
        }

        if (current is CampaignStatus.Completed or CampaignStatus.Cancelled)
        {
            throw new DomainException(
                "A completed or cancelled campaign cannot change status.",
                "CAMPAIGN_STATUS_LOCKED");
        }

        return requested;
    }

    private static string NormalizeCurrency(string? currency)
    {
        var value = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        return value;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildReference(DateTime now) =>
        $"DON-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static double Percent(decimal raised, decimal goal) =>
        goal <= 0
            ? 0
            : (double)Math.Round(
                raised / goal * 100m,
                1,
                MidpointRounding.AwayFromZero);

    private static void CompleteCampaignDto(CampaignDto campaign)
    {
        campaign.RaisedAmount = Round(campaign.RaisedAmount);
        campaign.SpentAmount = Round(campaign.SpentAmount);
        campaign.RemainingAmount = Round(campaign.RaisedAmount - campaign.SpentAmount);
        campaign.ProgressPercent = Percent(campaign.RaisedAmount, campaign.GoalAmount);
    }

    private static void CompleteDonationDto(DonationDto donation)
    {
        donation.AllocatableValue = donation.DonationType == DonationType.Monetary
            ? donation.Amount
            : donation.EstimatedValue;

        donation.UnallocatedAmount = donation.AllocatableValue.HasValue
            ? Round(donation.AllocatableValue.Value - donation.AllocatedAmount)
            : null;
    }
}
