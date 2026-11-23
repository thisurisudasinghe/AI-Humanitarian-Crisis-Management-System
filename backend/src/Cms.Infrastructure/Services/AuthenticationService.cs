using System.Security.Cryptography;
using System.Text;
using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Authentication;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Cms.Infrastructure.Services;

/// <summary>
/// Identity and access implementation for M1. Emails are encrypted at rest and
/// looked up by a deterministic HMAC; refresh tokens are stored as SHA-256 hashes.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly CmsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly JwtTokenService _jwtTokenService;
    private readonly EmailProtectionService _emailProtection;
    private readonly PasswordHashingService _passwordHashing;
    private readonly IDateTimeService _dateTimeService;

    public AuthenticationService(
        CmsDbContext db,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        JwtTokenService jwtTokenService,
        EmailProtectionService emailProtection,
        PasswordHashingService passwordHashing,
        IDateTimeService dateTimeService)
    {
        _db = db;
        _configuration = configuration;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _jwtTokenService = jwtTokenService;
        _emailProtection = emailProtection;
        _passwordHashing = passwordHashing;
        _dateTimeService = dateTimeService;
    }

    public async Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fullName = (request.FullName ?? string.Empty).Trim();
        var email = NormalizeEmail(request.Email);
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

        if (password.Length < 8 || password.Length > 128)
        {
            errors[nameof(request.Password)] = ["Password must be between 8 and 128 characters."];
        }

        var requestedPurpose = NormalizeAccountPurpose(request.AccountPurpose);
        if (requestedPurpose is not null && !IsAllowedPublicPurpose(requestedPurpose))
        {
            errors[nameof(request.AccountPurpose)] =
                ["Select a valid public account purpose."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Registration details are invalid.", errors);
        }

        var emailHash = _emailProtection.HashEmail(email);

        if (await _db.Users.AnyAsync(user => user.EmailHash == emailHash, cancellationToken))
        {
            throw new ConflictException(
                "An account with this email address already exists.",
                "EMAIL_ALREADY_EXISTS");
        }

        var roleNames = ResolvePublicRegistrationRoles(requestedPurpose);
        var roles = await _db.Roles
            .Where(role => roleNames.Contains(role.Name))
            .ToListAsync(cancellationToken);

        var missingRole = roleNames.FirstOrDefault(name => roles.All(role => role.Name != name));
        if (missingRole is not null)
        {
            throw new DomainException(
                $"The {missingRole} role has not been seeded.",
                "ROLE_NOT_CONFIGURED");
        }

        var now = _dateTimeService.UtcNow;
        var (firstName, lastName) = SplitName(fullName);

        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            EmailEncrypted = _emailProtection.EncryptEmail(email),
            EmailHash = emailHash,
            PasswordHash = _passwordHashing.HashPassword(new User(), password),
            IsEmailVerified = false,
            Status = UserStatus.Active,
            CreatedAt = now,
            UserRoles = roles.Select(role => new UserRole
            {
                RoleId = role.Id,
                AssignedAt = now
            }).ToList()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        if (roleNames.Contains(RoleNames.Donor))
        {
            await EnsureDonorProfileAsync(user, email, cancellationToken);
        }

        var response = await IssueTokensAsync(user, email, cancellationToken);

        await _auditLogger.LogAsync(
            user.Id,
            AuditActions.UserRegistered,
            "identity.User",
            user.Id.ToString(),
            new { email, accountPurpose = requestedPurpose ?? RoleNames.Citizen, roles = roleNames },
            cancellationToken);

        return response;
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeEmail(request.Email);
        var password = request.Password ?? string.Empty;

        if (!IsValidEmail(email) || password.Length == 0)
        {
            throw new ValidationException(
                "A valid email address and password are required.",
                new Dictionary<string, string[]>
                {
                    [nameof(request.Email)] = ["A valid email address is required."],
                    [nameof(request.Password)] = ["Password is required."]
                });
        }

        var emailHash = _emailProtection.HashEmail(email);

        var user = await _db.Users
            .Include(candidate => candidate.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(
                candidate => candidate.EmailHash == emailHash,
                cancellationToken);

        if (user is null ||
            !_passwordHashing.VerifyPassword(user, user.PasswordHash, password))
        {
            await _auditLogger.LogAsync(
                user?.Id,
                AuditActions.UserLoggedIn,
                "identity.User",
                user?.Id.ToString(),
                new { email, outcome = "FailedInvalidCredentials" },
                cancellationToken);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            await _auditLogger.LogAsync(
                user.Id,
                AuditActions.UserLoggedIn,
                "identity.User",
                user.Id.ToString(),
                new { email, outcome = "FailedInactiveAccount" },
                cancellationToken);

            throw new UnauthorizedAccessException("This account is not active.");
        }

        user.LastLoginAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var response = await IssueTokensAsync(user, email, cancellationToken);

        await _auditLogger.LogAsync(
            user.Id,
            AuditActions.UserLoggedIn,
            "identity.User",
            user.Id.ToString(),
            new { email, outcome = "Success" },
            cancellationToken);

        return response;
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ValidationException(
                "A refresh token is required.",
                new Dictionary<string, string[]>
                {
                    ["RefreshToken"] = ["A refresh token is required."]
                });
        }

        var presentedHash = HashRefreshToken(refreshToken);
        var now = _dateTimeService.UtcNow;

        var stored = await _db.RefreshTokens
            .Include(token => token.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(
                token => token.TokenHash == presentedHash,
                cancellationToken);

        if (stored is null)
        {
            await _auditLogger.LogAsync(
                null,
                "User.RefreshFailed",
                "identity.RefreshToken",
                null,
                new { outcome = "UnknownToken" },
                cancellationToken);

            throw new UnauthorizedAccessException("The refresh token is invalid.");
        }

        if (stored.IsRevoked || stored.ExpiresAt <= now)
        {
            // The token was already rotated. Revoke every live token for the user
            // so a stolen token cannot be replayed.
            var liveTokens = await _db.RefreshTokens
                .Where(token => token.UserId == stored.UserId && !token.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var liveToken in liveTokens)
            {
                liveToken.IsRevoked = true;
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _auditLogger.LogAsync(
                stored.UserId,
                "User.RefreshReuseDetected",
                "identity.RefreshToken",
                stored.Id.ToString(),
                new { outcome = "ChainRevoked" },
                cancellationToken);

            throw new UnauthorizedAccessException("The refresh token is no longer valid.");
        }

        if (stored.User.Status != UserStatus.Active)
        {
            throw new UnauthorizedAccessException("This account is not active.");
        }

        stored.IsRevoked = true;

        var replacement = CreateRefreshToken(stored.UserId, now, out var replacementToken);
        _db.RefreshTokens.Add(replacement);

        await _db.SaveChangesAsync(cancellationToken);

        var email = DecryptEmail(stored.User.EmailEncrypted);
        var response = BuildAuthResponse(stored.User, email, replacementToken);

        await _auditLogger.LogAsync(
            stored.UserId,
            "User.Refreshed",
            "identity.RefreshToken",
            replacement.Id.ToString(),
            new { outcome = "Rotated" },
            cancellationToken);

        return response;
    }

    public async Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = _currentUser.UserId;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ValidationException(
                "A refresh token is required.",
                new Dictionary<string, string[]>
                {
                    ["RefreshToken"] = ["A refresh token is required."]
                });
        }

        var presentedHash = HashRefreshToken(refreshToken);

        var stored = await _db.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.TokenHash == presentedHash,
                cancellationToken);

        if (stored is not null)
        {
            actorUserId ??= stored.UserId;

            if (!stored.IsRevoked)
            {
                stored.IsRevoked = true;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        await _auditLogger.LogAsync(
            actorUserId,
            AuditActions.UserLoggedOut,
            "identity.RefreshToken",
            stored?.Id.ToString(),
            new { outcome = stored is null ? "UnknownToken" : "Revoked" },
            cancellationToken);
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var user = await LoadUserAsync(
            query => query.Where(candidate => candidate.Id == userId),
            cancellationToken)
            ?? throw new UnauthorizedAccessException("The authenticated user no longer exists.");

        if (user.Status != UserStatus.Active)
        {
            throw new UnauthorizedAccessException("This account is not active.");
        }

        return new CurrentUserDto
        {
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = DecryptEmail(user.EmailEncrypted),
            Roles = GetRoleNames(user)
        };
    }

    public async Task<AuthResponseDto> EnrollPublicRoleAsync(
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var normalized = NormalizeAccountPurpose(purpose);
        if (normalized is null || !IsAllowedPublicPurpose(normalized))
        {
            throw new ValidationException(
                "Only public participation roles can be added to an existing account.",
                new Dictionary<string, string[]>
                {
                    [nameof(purpose)] = ["Select a valid public participation role."]
                });
        }

        var user = await _db.Users
            .Include(candidate => candidate.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("The authenticated user no longer exists.");

        if (user.Status != UserStatus.Active)
        {
            throw new UnauthorizedAccessException("This account is not active.");
        }

        var email = DecryptEmail(user.EmailEncrypted);

        // Idempotent: re-selecting an existing participation role changes no
        // role, but still returns fresh tokens so the caller sees the role
        // immediately in the current session.
        if (user.UserRoles.Any(userRole =>
                userRole.Role is not null && userRole.Role.Name == normalized))
        {
            return await IssueTokensAsync(user, email, cancellationToken);
        }

        var role = await _db.Roles
            .SingleOrDefaultAsync(candidate => candidate.Name == normalized, cancellationToken)
            ?? throw new DomainException(
                $"The {normalized} role has not been seeded.",
                "ROLE_NOT_CONFIGURED");

        user.UserRoles.Add(new UserRole
        {
            RoleId = role.Id,
            Role = role,
            UserId = user.Id,
            AssignedAt = _dateTimeService.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        if (normalized == RoleNames.Donor)
        {
            await EnsureDonorProfileAsync(user, email, cancellationToken);
        }

        await _auditLogger.LogAsync(
            user.Id,
            AuditActions.UserRoleChanged,
            "identity.User",
            user.Id.ToString(),
            new { role = normalized, source = "PublicPortal" },
            cancellationToken);

        // Access-token claims are immutable, so the enrolment response must
        // carry a freshly signed token for the new role to take effect.
        return await IssueTokensAsync(user, email, cancellationToken);
    }

    private async Task<AuthResponseDto> IssueTokensAsync(
        User user,
        string email,
        CancellationToken cancellationToken)
    {
        var refreshToken = CreateRefreshToken(
            user.Id,
            _dateTimeService.UtcNow,
            out var plainToken);

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, email, plainToken);
    }

    private AuthResponseDto BuildAuthResponse(
        User user,
        string email,
        string refreshToken)
    {
        var roles = GetRoleNames(user);
        var (accessToken, expiresAt) = _jwtTokenService.GenerateAccessToken(
            user.Id,
            email,
            roles,
            user.OrganisationId);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = email,
            Roles = roles
        };
    }

    private async Task<User?> LoadUserAsync(
        Func<IQueryable<User>, IQueryable<User>> filter,
        CancellationToken cancellationToken)
    {
        var query = _db.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
                .ThenInclude(userRole => userRole.Role);

        return await filter(query).SingleOrDefaultAsync(cancellationToken);
    }

    private RefreshToken CreateRefreshToken(
        Guid userId,
        DateTime now,
        out string plainToken)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        plainToken = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var expirationDays = _configuration
            .GetSection("Jwt")
            .GetValue<int?>("RefreshTokenExpirationDays") ?? 7;

        return new RefreshToken
        {
            UserId = userId,
            TokenHash = HashRefreshToken(plainToken),
            ExpiresAt = now.AddDays(expirationDays),
            IsRevoked = false,
            CreatedAt = now
        };
    }

    private static string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

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

    private static string? NormalizeAccountPurpose(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "citizen" or "request-assistance" or "requestassistance" => RoleNames.Citizen,
            "volunteer" or "offer-help" or "offerhelp" => RoleNames.Volunteer,
            "donor" or "support-relief" or "supportrelief" or "support-relief-efforts" => RoleNames.Donor,
            _ => value.Trim()
        };
    }

    private static bool IsAllowedPublicPurpose(string purpose) =>
        RoleNames.PublicSelfRegistrationRoles.Contains(purpose, StringComparer.Ordinal);

    private static IReadOnlyCollection<string> ResolvePublicRegistrationRoles(string? purpose)
    {
        // Citizen remains the baseline public account role so existing case,
        // notification, household and public-assistance workflows keep working.
        var roles = new List<string> { RoleNames.Citizen };
        if (purpose is RoleNames.Volunteer or RoleNames.Donor && !roles.Contains(purpose))
        {
            roles.Add(purpose);
        }

        return roles;
    }

    private async Task EnsureDonorProfileAsync(
        User user,
        string email,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Donors
            .AnyAsync(donor => donor.UserId == user.Id, cancellationToken);

        if (exists)
        {
            return;
        }

        _db.Donors.Add(new Donor
        {
            Id = Guid.NewGuid(),
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            DonorType = DonorType.Individual,
            ContactEmail = email,
            IsAnonymous = false,
            IsActive = true,
            UserId = user.Id,
            CreatedByUserId = user.Id,
            CreatedAt = _dateTimeService.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyCollection<string> GetRoleNames(User user) =>
        user.UserRoles
            .Where(userRole => userRole.Role is not null &&
                               !string.IsNullOrWhiteSpace(userRole.Role.Name))
            .Select(userRole => userRole.Role.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        return parts.Length == 1
            ? (parts[0], string.Empty)
            : (parts[0], string.Join(' ', parts.Skip(1)));
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        if (email.Length is < 3 or > 254)
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@') || atIndex == email.Length - 1)
        {
            return false;
        }

        return email.IndexOf('.', atIndex + 1) >= 0;
    }
}
