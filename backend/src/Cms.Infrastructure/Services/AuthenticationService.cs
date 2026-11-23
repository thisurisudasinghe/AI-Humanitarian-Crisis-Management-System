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
using Cms.Infrastructure.Notifications;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly IEmailVerificationEmailSender _verificationEmailSender;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        CmsDbContext db,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        JwtTokenService jwtTokenService,
        EmailProtectionService emailProtection,
        PasswordHashingService passwordHashing,
        IDateTimeService dateTimeService,
        IEmailVerificationEmailSender verificationEmailSender,
        ILogger<AuthenticationService> logger)
    {
        _db = db;
        _configuration = configuration;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _jwtTokenService = jwtTokenService;
        _emailProtection = emailProtection;
        _passwordHashing = passwordHashing;
        _dateTimeService = dateTimeService;
        _verificationEmailSender = verificationEmailSender;
        _logger = logger;
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

        var citizenRole = await _db.Roles
            .SingleOrDefaultAsync(role => role.Name == RoleNames.Citizen, cancellationToken)
            ?? throw new DomainException(
                "The Citizen role has not been seeded.",
                "ROLE_NOT_CONFIGURED");

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
            UserRoles =
            [
                new UserRole
                {
                    RoleId = citizenRole.Id,
                    AssignedAt = now
                }
            ]
        };

        var (rawToken, tokenEntity) = CreateEmailVerificationToken(user.Id, now);

        _db.Users.Add(user);
        _db.EmailVerificationTokens.Add(tokenEntity);
        await _db.SaveChangesAsync(cancellationToken);

        // The raw token is only ever handed to the email sender. Registration
        // still succeeds if the provider is unavailable; the user can resend.
        try
        {
            await SendVerificationEmailAsync(
                user,
                email,
                rawToken,
                isResend: false,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Verification email was not delivered for a newly registered user.");
        }

        var response = await IssueTokensAsync(user, email, cancellationToken);

        await _auditLogger.LogAsync(
            user.Id,
            AuditActions.UserRegistered,
            "identity.User",
            user.Id.ToString(),
            new { email },
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
            Roles = GetRoleNames(user),
            IsEmailVerified = user.IsEmailVerified
        };
    }

    public async Task<EmailVerificationResponseDto> VerifyEmailAsync(
        VerifyEmailRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawToken = (request.Token ?? string.Empty).Trim();
        if (rawToken.Length is 0 or > 200)
        {
            throw new ValidationException(
                "A verification token is required.",
                new Dictionary<string, string[]>
                {
                    ["Token"] = ["A verification token is required."]
                });
        }

        var tokenHash = HashVerificationToken(rawToken);
        var now = _dateTimeService.UtcNow;

        var stored = await _db.EmailVerificationTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);

        if (stored is null)
        {
            throw new DomainException(
                "This verification link is not valid.",
                "VERIFICATION_TOKEN_INVALID");
        }

        if (stored.ConsumedAt.HasValue)
        {
            await LogVerificationAuditAsync(
                stored.UserId,
                "AlreadyUsed",
                cancellationToken);

            throw new DomainException(
                "This verification link has already been used.",
                "VERIFICATION_TOKEN_ALREADY_USED");
        }

        if (stored.User.IsEmailVerified)
        {
            stored.ConsumedAt = now;
            await _db.SaveChangesAsync(cancellationToken);

            await LogVerificationAuditAsync(
                stored.UserId,
                "AlreadyVerified",
                cancellationToken);

            return new EmailVerificationResponseDto
            {
                IsEmailVerified = true,
                AlreadyVerified = true,
                Message = "Your email address is already verified."
            };
        }

        if (stored.RevokedAt.HasValue)
        {
            throw new DomainException(
                "This verification link has been replaced. Request a new one.",
                "VERIFICATION_TOKEN_REVOKED");
        }

        if (stored.ExpiresAt <= now)
        {
            throw new DomainException(
                "This verification link has expired. Request a new one.",
                "VERIFICATION_TOKEN_EXPIRED");
        }

        // Optimistic concurrency on consumed_at/revoked_at makes redemption
        // single-use even if two requests load the same token simultaneously.
        stored.ConsumedAt = now;
        stored.User.IsEmailVerified = true;
        stored.User.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            var settled = await _db.EmailVerificationTokens
                .AsNoTracking()
                .Include(token => token.User)
                .SingleOrDefaultAsync(token => token.Id == stored.Id, cancellationToken);

            if (settled?.ConsumedAt.HasValue == true || settled?.User.IsEmailVerified == true)
            {
                await LogVerificationAuditAsync(
                    stored.UserId,
                    "ConcurrentVerificationLost",
                    cancellationToken);

                throw new DomainException(
                    "This verification link has already been used.",
                    "VERIFICATION_TOKEN_ALREADY_USED");
            }

            throw new DomainException(
                "This verification link is not valid.",
                "VERIFICATION_TOKEN_INVALID");
        }

        await LogVerificationAuditAsync(stored.UserId, "Verified", cancellationToken);

        return new EmailVerificationResponseDto
        {
            IsEmailVerified = true,
            AlreadyVerified = false,
            Message = "Your email address has been verified."
        };
    }

    public async Task<EmailVerificationResponseDto> ResendVerificationAsync(
        ResendVerificationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = NormalizeEmail(request.Email);
        if (!IsValidEmail(email))
        {
            throw new ValidationException(
                "A valid email address is required.",
                new Dictionary<string, string[]>
                {
                    ["Email"] = ["A valid email address is required."]
                });
        }

        // One safe response for unknown, inactive and already-verified accounts
        // so this anonymous endpoint cannot be used to enumerate users.
        var safeResponse = new EmailVerificationResponseDto
        {
            IsEmailVerified = false,
            AlreadyVerified = false,
            Message = "If an account exists that still needs verification, a new link has been sent."
        };

        var emailHash = _emailProtection.HashEmail(email);
        var user = await _db.Users
            .SingleOrDefaultAsync(candidate => candidate.EmailHash == emailHash, cancellationToken);

        if (user is null || user.Status != UserStatus.Active || user.IsEmailVerified)
        {
            return safeResponse;
        }

        var now = _dateTimeService.UtcNow;
        var cooldownSeconds = Math.Max(
            0,
            _configuration.GetSection(EmailVerificationOptions.SectionName)
                .GetValue<int?>("ResendCooldownSeconds") ?? 60);

        var latest = await _db.EmailVerificationTokens
            .Where(token => token.UserId == user.Id)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.CreatedAt > now.AddSeconds(-cooldownSeconds))
        {
            return safeResponse;
        }

        var activeTokens = await _db.EmailVerificationTokens
            .Where(token => token.UserId == user.Id &&
                            token.ConsumedAt == null &&
                            token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = now;
        }

        var (rawToken, tokenEntity) = CreateEmailVerificationToken(user.Id, now);
        _db.EmailVerificationTokens.Add(tokenEntity);

        try
        {
            // The filtered unique index in SQL also rejects two simultaneous
            // active tokens; the safe response prevents caller-visible races.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or DbUpdateException)
        {
            return safeResponse;
        }

        await SendVerificationEmailAsync(user, email, rawToken, isResend: true, cancellationToken);

        await _auditLogger.LogAsync(
            user.Id,
            "User.EmailVerificationResent",
            "identity.User",
            user.Id.ToString(),
            new { outcome = "Sent" },
            cancellationToken);

        return safeResponse;
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
            user.OrganisationId,
            user.IsEmailVerified);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAt = expiresAt,
            UserId = user.Id,
            FullName = $"{user.FirstName} {user.LastName}".Trim(),
            Email = email,
            Roles = roles,
            IsEmailVerified = user.IsEmailVerified
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

    private static string HashVerificationToken(string token) => HashRefreshToken(token);

    private (string Token, EmailVerificationToken Entity) CreateEmailVerificationToken(
        Guid userId,
        DateTime now)
    {
        // 32 cryptographically secure bytes, Base64URL encoded (43 characters).
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var lifetimeMinutes = Math.Max(
            1,
            _configuration.GetSection(EmailVerificationOptions.SectionName)
                .GetValue<int?>("TokenExpirationMinutes") ?? 30);

        var entity = new EmailVerificationToken
        {
            UserId = userId,
            TokenHash = HashVerificationToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(lifetimeMinutes)
        };

        return (rawToken, entity);
    }

    private async Task SendVerificationEmailAsync(
        User user,
        string email,
        string rawToken,
        bool isResend,
        CancellationToken cancellationToken)
    {
        var baseUrl = RequireHttpsSetting(
            _configuration[EmailVerificationOptions.SectionName + ":BaseUrl"],
            "EMAIL_VERIFICATION_BASE_URL");

        var logoUrl = RequireHttpsSetting(
            _configuration[EmailVerificationOptions.SectionName + ":LogoUrl"],
            "EMAIL_LOGO_URL");

        var verificationUrl = $"{baseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(rawToken)}";

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (displayName.Length == 0)
        {
            displayName = "there";
        }

        var (html, text) = isResend
            ? EmailVerificationTemplates.RenderResend(displayName, logoUrl, verificationUrl)
            : EmailVerificationTemplates.RenderRegistration(displayName, logoUrl, verificationUrl);

        var subject = isResend
            ? EmailVerificationTemplates.ResendSubject
            : EmailVerificationTemplates.RegisterSubject;

        var fromName = _configuration["Resend:FromName"];
        if (string.IsNullOrWhiteSpace(fromName))
        {
            fromName = "Safeway";
        }

        await _verificationEmailSender.SendAsync(
            email,
            displayName,
            fromName.Trim(),
            subject,
            html,
            text,
            cancellationToken);
    }

    private async Task LogVerificationAuditAsync(
        Guid userId,
        string outcome,
        CancellationToken cancellationToken)
    {
        await _auditLogger.LogAsync(
            userId,
            "User.EmailVerification",
            "identity.User",
            userId.ToString(),
            new { outcome },
            cancellationToken);
    }

    private static string RequireHttpsSetting(string? value, string environmentVariable)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"{environmentVariable} must be configured as an absolute HTTPS URL.");
        }

        return value!;
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
