using System.Security.Cryptography;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Admin;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services;

/// <summary>SystemAdmin operations for users, organisations and configuration.</summary>
public sealed class AdminService : IAdminService
{
    private readonly CmsDbContext _db;
    private readonly IAuditLogger _auditLogger;
    private readonly EmailProtectionService _emailProtection;
    private readonly PasswordHashingService _passwordHashing;
    private readonly IDateTimeService _dateTimeService;

    public AdminService(
        CmsDbContext db,
        IAuditLogger auditLogger,
        EmailProtectionService emailProtection,
        PasswordHashingService passwordHashing,
        IDateTimeService dateTimeService)
    {
        _db = db;
        _auditLogger = auditLogger;
        _emailProtection = emailProtection;
        _passwordHashing = passwordHashing;
        _dateTimeService = dateTimeService;
    }

    public async Task<PagedResultDto<UserSummaryDto>> GetUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);

        var query = UserQuery();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            var emailHash = term.Contains('@')
                ? _emailProtection.HashEmail(term)
                : null;

            query = query.Where(user =>
                user.FirstName.ToLower().Contains(term) ||
                user.LastName.ToLower().Contains(term) ||
                (user.FirstName + " " + user.LastName).ToLower().Contains(term) ||
                (emailHash != null && user.EmailHash == emailHash));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleName = role.Trim();
            query = query.Where(user =>
                user.UserRoles.Any(userRole => userRole.Role.Name == roleName));
        }

        if (isActive.HasValue)
        {
            query = isActive.Value
                ? query.Where(user => user.Status == UserStatus.Active)
                : query.Where(user => user.Status != UserStatus.Active);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<UserSummaryDto>
        {
            Items = users.Select(MapUser).ToArray(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UserSummaryDto> GetUserAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await UserQuery()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.", "USER_NOT_FOUND");

        return MapUser(user);
    }

    public async Task<UserSummaryDto> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fullName = (request.FullName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var password = request.Password ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (fullName.Length == 0)
        {
            errors[nameof(request.FullName)] = ["Full name is required."];
        }

        if (!IsValidEmail(email))
        {
            errors[nameof(request.Email)] = ["A valid email address is required."];
        }

        if (password.Length is < 8 or > 128)
        {
            errors[nameof(request.Password)] = ["Password must be between 8 and 128 characters."];
        }

        if (request.Roles.Count == 0)
        {
            errors[nameof(request.Roles)] = ["At least one role is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("User details are invalid.", errors);
        }

        var emailHash = _emailProtection.HashEmail(email);
        if (await _db.Users.AnyAsync(user => user.EmailHash == emailHash, cancellationToken))
        {
            throw new ConflictException(
                "An account with this email address already exists.",
                "EMAIL_ALREADY_EXISTS");
        }

        if (request.OrganisationId.HasValue &&
            !await _db.Organisations.AnyAsync(
                organisation => organisation.Id == request.OrganisationId.Value,
                cancellationToken))
        {
            throw new ValidationException(
                "The selected organisation does not exist.",
                new Dictionary<string, string[]>
                {
                    [nameof(request.OrganisationId)] = ["Organisation was not found."]
                });
        }

        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);
        var (firstName, lastName) = SplitName(fullName);
        var now = _dateTimeService.UtcNow;

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            EmailEncrypted = _emailProtection.EncryptEmail(email),
            EmailHash = emailHash,
            PasswordHash = _passwordHashing.HashPassword(new User(), password),
            OrganisationId = request.OrganisationId,
            Status = UserStatus.Active,
            CreatedAt = now,
            UserRoles = roles
                .Select(role => new UserRole { RoleId = role.Id, AssignedAt = now })
                .ToList()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.UserRegistered,
            "identity.User",
            user.Id.ToString(),
            new { email, roles = roles.Select(role => role.Name).ToArray() },
            cancellationToken);

        return (await GetUserAsync(user.Id, cancellationToken));
    }

    public async Task<UserSummaryDto> UpdateUserAsync(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _db.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.", "USER_NOT_FOUND");

        var previousStatus = user.Status;

        if (request.FirstName is not null)
        {
            var firstName = request.FirstName.Trim();
            if (firstName.Length == 0)
            {
                throw new ValidationException("First name cannot be empty.");
            }

            user.FirstName = firstName;
        }

        if (request.LastName is not null)
        {
            user.LastName = request.LastName.Trim();
        }

        if (request.OrganisationId.HasValue)
        {
            if (request.OrganisationId.Value != Guid.Empty &&
                !await _db.Organisations.AnyAsync(
                    organisation => organisation.Id == request.OrganisationId.Value,
                    cancellationToken))
            {
                throw new ValidationException("The selected organisation does not exist.");
            }

            user.OrganisationId = request.OrganisationId.Value == Guid.Empty
                ? null
                : request.OrganisationId.Value;
        }

        if (request.Status.HasValue)
        {
            user.Status = request.Status.Value;
        }

        user.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var action = user.Status == UserStatus.Active
            ? AuditActions.UserUpdated
            : AuditActions.UserDeactivated;

        await _auditLogger.LogAsync(
            action,
            "identity.User",
            user.Id.ToString(),
            new
            {
                previousStatus = previousStatus.ToString(),
                status = user.Status.ToString(),
                organisationId = user.OrganisationId
            },
            cancellationToken);

        return await GetUserAsync(id, cancellationToken);
    }

    public async Task<UserSummaryDto> ReplaceUserRolesAsync(
        Guid id,
        ReplaceRolesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Roles.Count == 0)
        {
            throw new ValidationException("At least one role is required.");
        }

        var user = await _db.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.", "USER_NOT_FOUND");

        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);
        var now = _dateTimeService.UtcNow;

        _db.UserRoles.RemoveRange(
            await _db.UserRoles
                .Where(userRole => userRole.UserId == id)
                .ToListAsync(cancellationToken));

        _db.UserRoles.AddRange(
            roles.Select(role => new UserRole
            {
                UserId = id,
                RoleId = role.Id,
                AssignedAt = now
            }));

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.UserRoleChanged,
            "identity.User",
            id.ToString(),
            new { roles = roles.Select(role => role.Name).ToArray() },
            cancellationToken);

        return await GetUserAsync(id, cancellationToken);
    }

    public async Task<PagedResultDto<OrganisationDto>> GetOrganisationsAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Organisations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(organisation => organisation.Name.ToLower().Contains(term));
        }

        if (isActive.HasValue)
        {
            query = query.Where(organisation => organisation.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var organisations = await query
            .OrderBy(organisation => organisation.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<OrganisationDto>
        {
            Items = organisations.Select(MapOrganisation).ToArray(),
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount
        };
    }

    public async Task<OrganisationDto> GetOrganisationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var organisation = await _db.Organisations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("Organisation was not found.", "ORGANISATION_NOT_FOUND");

        return MapOrganisation(organisation);
    }

    public async Task<OrganisationDto> CreateOrganisationAsync(
        CreateOrganisationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new ValidationException("Organisation name is required.");
        }

        var organisation = new Organisation
        {
            Name = name,
            OrganisationType = request.OrganisationType,
            RegistrationNumber = request.RegistrationNumber,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            Address = request.Address,
            IsActive = true,
            CreatedAt = _dateTimeService.UtcNow
        };

        _db.Organisations.Add(organisation);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.OrganisationCreated,
            "common.Organisation",
            organisation.Id.ToString(),
            new { name },
            cancellationToken);

        return MapOrganisation(organisation);
    }

    public async Task<OrganisationDto> UpdateOrganisationAsync(
        Guid id,
        UpdateOrganisationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var organisation = await _db.Organisations
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new NotFoundException("Organisation was not found.", "ORGANISATION_NOT_FOUND");

        var previousActive = organisation.IsActive;

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0)
            {
                throw new ValidationException("Organisation name cannot be empty.");
            }

            organisation.Name = name;
        }

        if (request.OrganisationType.HasValue)
        {
            organisation.OrganisationType = request.OrganisationType.Value;
        }

        if (request.RegistrationNumber is not null)
        {
            organisation.RegistrationNumber = request.RegistrationNumber;
        }

        if (request.ContactEmail is not null)
        {
            organisation.ContactEmail = request.ContactEmail;
        }

        if (request.ContactPhone is not null)
        {
            organisation.ContactPhone = request.ContactPhone;
        }

        if (request.Address is not null)
        {
            organisation.Address = request.Address;
        }

        if (request.IsActive.HasValue)
        {
            organisation.IsActive = request.IsActive.Value;
        }

        organisation.UpdatedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var action = !previousActive && organisation.IsActive
            ? AuditActions.OrganisationUpdated
            : organisation.IsActive
                ? AuditActions.OrganisationUpdated
                : AuditActions.OrganisationDeactivated;

        await _auditLogger.LogAsync(
            action,
            "common.Organisation",
            organisation.Id.ToString(),
            new
            {
                previousIsActive = previousActive,
                isActive = organisation.IsActive,
                name = organisation.Name
            },
            cancellationToken);

        return MapOrganisation(organisation);
    }

    public async Task<IReadOnlyCollection<SystemSettingDto>> GetConfigAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.SystemSettings
            .AsNoTracking()
            .OrderBy(setting => setting.Key)
            .ToListAsync(cancellationToken);

        return settings.Select(MapSetting).ToArray();
    }

    public async Task<IReadOnlyCollection<SystemSettingDto>> UpdateConfigAsync(
        UpdateConfigRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Settings.Count == 0)
        {
            throw new ValidationException("At least one setting is required.");
        }

        var errors = new Dictionary<string, string[]>();
        foreach (var setting in request.Settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Key))
            {
                errors["Key"] = ["Setting keys cannot be empty."];
            }
            else if (!SystemSettingKeys.All.Contains(setting.Key, StringComparer.Ordinal))
            {
                errors[setting.Key] = ["Unknown setting key."];
            }

            if (setting.Value is null)
            {
                errors[setting.Key ?? "Value"] = ["Setting values cannot be null."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Configuration values are invalid.", errors);
        }

        var changedKeys = new List<string>();
        var now = _dateTimeService.UtcNow;

        foreach (var incoming in request.Settings)
        {
            var existing = await _db.SystemSettings
                .SingleOrDefaultAsync(setting => setting.Key == incoming.Key, cancellationToken);

            if (existing is null)
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Key = incoming.Key,
                    Value = incoming.Value,
                    Description = incoming.Description,
                    CreatedAt = now
                });
            }
            else
            {
                existing.Value = incoming.Value;
                existing.Description = incoming.Description;
                existing.UpdatedAt = now;
            }

            changedKeys.Add(incoming.Key);
        }

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            AuditActions.ConfigUpdated,
            "common.SystemSetting",
            null,
            new { keys = changedKeys.Distinct(StringComparer.Ordinal).ToArray() },
            cancellationToken);

        return await GetConfigAsync(cancellationToken);
    }

    private IQueryable<User> UserQuery() =>
        _db.Users
            .AsNoTracking()
            .Include(user => user.Organisation)
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role);

    private async Task<IReadOnlyCollection<Role>> ResolveRolesAsync(
        IEnumerable<string> roleNames,
        CancellationToken cancellationToken)
    {
        var names = roleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var invalid = names
            .Where(name => !RoleNames.All.Contains(name, StringComparer.Ordinal))
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new ValidationException(
                "Unknown or unsupported role.",
                new Dictionary<string, string[]>
                {
                    ["Roles"] = [$"Unsupported roles: {string.Join(", ", invalid)}"]
                });
        }

        var roles = await _db.Roles
            .Where(role => names.Contains(role.Name))
            .ToListAsync(cancellationToken);

        var missing = names
            .Except(roles.Select(role => role.Name), StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new ValidationException(
                "A role has not been seeded.",
                new Dictionary<string, string[]>
                {
                    ["Roles"] = [$"Missing roles: {string.Join(", ", missing)}"]
                },
                "ROLE_NOT_CONFIGURED");
        }

        return roles;
    }

    private UserSummaryDto MapUser(User user) =>
        new()
        {
            Id = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = DecryptEmail(user.EmailEncrypted),
            OrganisationId = user.OrganisationId,
            OrganisationName = user.Organisation?.Name,
            Roles = user.UserRoles
                .Where(userRole => userRole.Role is not null)
                .Select(userRole => userRole.Role.Name)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Status = user.Status,
            LastLoginAt = user.LastLoginAt
        };

    private static OrganisationDto MapOrganisation(Organisation organisation) =>
        new()
        {
            Id = organisation.Id,
            Name = organisation.Name,
            OrganisationType = organisation.OrganisationType,
            RegistrationNumber = organisation.RegistrationNumber,
            ContactEmail = organisation.ContactEmail,
            ContactPhone = organisation.ContactPhone,
            Address = organisation.Address,
            IsActive = organisation.IsActive
        };

    private static SystemSettingDto MapSetting(SystemSetting setting) =>
        new()
        {
            Id = setting.Id,
            Key = setting.Key,
            Value = setting.Value,
            Description = setting.Description
        };

    private string DecryptEmail(string? encryptedEmail)
    {
        if (string.IsNullOrWhiteSpace(encryptedEmail))
        {
            return string.Empty;
        }

        try
        {
            return _emailProtection.DecryptEmail(encryptedEmail);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], string.Join(' ', parts.Skip(1)))
        };
    }

    private static bool IsValidEmail(string email)
    {
        if (email.Length is < 3 or > 254)
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        return atIndex > 0 &&
               atIndex == email.LastIndexOf('@') &&
               atIndex < email.Length - 1 &&
               email.IndexOf('.', atIndex + 1) >= 0;
    }
}
