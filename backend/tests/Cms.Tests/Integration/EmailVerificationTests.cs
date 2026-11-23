using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Notifications;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Integration;

/// <summary>
/// End-to-end coverage of the Safeway email-verification flow. Resend is
/// replaced by an in-memory sender; no automated test sends a real message.
/// </summary>
public sealed class EmailVerificationTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public EmailVerificationTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Registration_creates_unverified_account_stores_only_hash_and_sends_email()
    {
        await _factory.EnsureSeededAsync();
        var email = NewEmail();
        var before = _factory.VerificationSender.Messages.Count;

        var (_, auth, responseBody) = await RegisterAsync(email, "New Citizen");

        Assert.NotNull(auth);
        Assert.False(auth!.IsEmailVerified);

        var message = LatestMessage(before);
        Assert.Equal(email, message.Email);
        Assert.Equal("New Citizen", message.DisplayName);
        Assert.Equal("Safeway", message.FromName);
        Assert.Equal(EmailVerificationTemplates.RegisterSubject, message.Subject);

        var rawToken = ExtractToken(message.Text);
        Assert.Matches(@"^[A-Za-z0-9_-]{43}$", rawToken);
        Assert.DoesNotContain(rawToken, responseBody, StringComparison.Ordinal);

        var hash = Hash(rawToken);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var token = await db.EmailVerificationTokens
            .SingleAsync(candidate => candidate.UserId == auth.UserId);

        Assert.Equal(hash, token.TokenHash);
        Assert.NotEqual(rawToken, token.TokenHash);
        Assert.Null(token.ConsumedAt);
        Assert.Null(token.RevokedAt);
        Assert.Equal(TimeSpan.FromMinutes(30), token.ExpiresAt - token.CreatedAt);

        var user = await db.Users.SingleAsync(candidate => candidate.Id == auth.UserId);
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Verification_tokens_are_unique_per_registration()
    {
        await _factory.EnsureSeededAsync();
        var before = _factory.VerificationSender.Messages.Count;

        var (_, first, _) = await RegisterAsync(NewEmail(), "First Citizen");
        var (_, second, _) = await RegisterAsync(NewEmail(), "Second Citizen");

        Assert.NotNull(first);
        Assert.NotNull(second);
        var messages = _factory.VerificationSender.Messages.Skip(before).ToArray();
        Assert.Equal(2, messages.Length);

        var firstToken = ExtractToken(messages[0].Text);
        var secondToken = ExtractToken(messages[1].Text);
        Assert.NotEqual(firstToken, secondToken);
        Assert.NotEqual(Hash(firstToken), Hash(secondToken));
    }

    [Fact]
    public async Task Registration_email_uses_approved_branding_and_plain_text_fallback()
    {
        await _factory.EnsureSeededAsync();
        var before = _factory.VerificationSender.Messages.Count;
        await RegisterAsync(NewEmail(), "Branded Citizen");

        var message = LatestMessage(before);
        Assert.Contains("https://localhost:3000/safeway-email-logo.svg", message.Html);
        Assert.Contains("alt=\"Safeway\"", message.Html);
        Assert.Contains("#438fc5", message.Html.ToLowerInvariant());
        Assert.Contains("#24577a", message.Html.ToLowerInvariant());
        Assert.Contains("#17364f", message.Html.ToLowerInvariant());
        Assert.Contains("Blue for Trust. White for Clarity. Green for Hope.", message.Html);
        Assert.Contains("max-width: 620px", message.Html);

        Assert.Contains("Verify My Email:", message.Text);
        Assert.Contains("https://localhost:3000/verify-email/confirm?token=", message.Text);
        Assert.DoesNotContain("C:\\", message.Html);
        Assert.DoesNotContain("/Users/", message.Html);
        Assert.DoesNotContain("{{", message.Html);
        Assert.DoesNotContain("{{", message.Text);
    }

    [Fact]
    public async Task Registration_does_not_log_raw_verification_token()
    {
        await _factory.EnsureSeededAsync();
        var before = _factory.VerificationSender.Messages.Count;
        await RegisterAsync(NewEmail(), "Quiet Citizen");

        var rawToken = ExtractToken(LatestMessage(before).Text);
        Assert.DoesNotContain(
            _factory.LogMessages,
            message => message.Contains(rawToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Registration_issues_restricted_session_that_cannot_submit_requests()
    {
        await _factory.EnsureSeededAsync();
        var (_, auth, _) = await RegisterAsync(NewEmail(), "Restricted Citizen");
        Assert.NotNull(auth);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth!.AccessToken);
        Assert.Equal("false", jwt.Claims.Single(claim => claim.Type == "email_verified").Value);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/requests", ValidRequest());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("EMAIL_VERIFICATION_REQUIRED", await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task Valid_token_marks_user_verified_consumes_token_and_updates_auth_dtos()
    {
        await _factory.EnsureSeededAsync();
        var email = NewEmail();
        var before = _factory.VerificationSender.Messages.Count;
        var (_, auth, _) = await RegisterAsync(email, "Verify Citizen");
        Assert.NotNull(auth);

        var rawToken = ExtractToken(LatestMessage(before).Text);
        using (var client = _factory.CreateClient())
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/email/verify",
                new { token = rawToken });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<EmailVerificationResponseDto>();
            Assert.NotNull(result);
            Assert.True(result!.IsEmailVerified);
            Assert.False(result.AlreadyVerified);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var token = await db.EmailVerificationTokens
                .SingleAsync(candidate => candidate.UserId == auth!.UserId);
            Assert.NotNull(token.ConsumedAt);

            var user = await db.Users.SingleAsync(candidate => candidate.Id == auth!.UserId);
            Assert.True(user.IsEmailVerified);
        }

        var login = await _factory.LoginAsync(email);
        Assert.True(login.IsEmailVerified);
        var loginJwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        Assert.Equal("true", loginJwt.Claims.Single(claim => claim.Type == "email_verified").Value);

        using var meClient = await _factory.CreateAuthenticatedClientAsync(email);
        var me = await meClient.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.True(me!.IsEmailVerified);

        using var requestClient = _factory.CreateClient();
        requestClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login.AccessToken);
        var submit = await requestClient.PostAsJsonAsync("/api/v1/requests", ValidRequest());
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
    }

    [Fact]
    public async Task Consumed_token_is_rejected_with_a_specific_code()
    {
        await _factory.EnsureSeededAsync();
        var before = _factory.VerificationSender.Messages.Count;
        await RegisterAsync(NewEmail(), "Replay Citizen");
        var rawToken = ExtractToken(LatestMessage(before).Text);

        using var client = _factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token = rawToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token = rawToken });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("VERIFICATION_TOKEN_ALREADY_USED", await GetErrorCodeAsync(replay));
    }

    [Fact]
    public async Task Invalid_missing_expired_and_revoked_tokens_have_distinct_problem_codes()
    {
        await _factory.EnsureSeededAsync();

        using var client = _factory.CreateClient();

        var missing = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        Assert.Equal("VALIDATION_FAILED", await GetErrorCodeAsync(missing));

        var invalid = await client.PostAsJsonAsync(
            "/api/v1/auth/email/verify",
            new { token = "not-a-real-verification-token" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("VERIFICATION_TOKEN_INVALID", await GetErrorCodeAsync(invalid));

        var expiredEmail = NewEmail();
        var beforeExpired = _factory.VerificationSender.Messages.Count;
        var (_, expiredAuth, _) = await RegisterAsync(expiredEmail, "Expired Citizen");
        var expiredToken = ExtractToken(LatestMessage(beforeExpired).Text);
        await MutateActiveTokenAsync(expiredAuth!.UserId, token =>
            token.ExpiresAt = DateTime.UtcNow.AddMinutes(-1));

        var expired = await client.PostAsJsonAsync(
            "/api/v1/auth/email/verify",
            new { token = expiredToken });
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        Assert.Equal("VERIFICATION_TOKEN_EXPIRED", await GetErrorCodeAsync(expired));

        var revokedEmail = NewEmail();
        var beforeRevoked = _factory.VerificationSender.Messages.Count;
        var (_, revokedAuth, _) = await RegisterAsync(revokedEmail, "Revoked Citizen");
        var revokedToken = ExtractToken(LatestMessage(beforeRevoked).Text);
        await MutateActiveTokenAsync(revokedAuth!.UserId, token =>
            token.RevokedAt = DateTime.UtcNow);

        var revoked = await client.PostAsJsonAsync(
            "/api/v1/auth/email/verify",
            new { token = revokedToken });
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
        Assert.Equal("VERIFICATION_TOKEN_REVOKED", await GetErrorCodeAsync(revoked));
    }

    [Fact]
    public async Task Resend_issues_new_token_revokes_previous_and_uses_resend_template()
    {
        await _factory.EnsureSeededAsync();
        var email = NewEmail();
        var before = _factory.VerificationSender.Messages.Count;
        var (_, auth, _) = await RegisterAsync(email, "Resend Citizen");
        Assert.NotNull(auth);
        var firstToken = ExtractToken(LatestMessage(before).Text);
        var messageCountBeforeResend = _factory.VerificationSender.Messages.Count;

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/email/resend",
            new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<EmailVerificationResponseDto>();
        Assert.NotNull(result);
        Assert.False(result!.IsEmailVerified);
        Assert.Contains("new link has been sent", result.Message);

        var resendMessage = LatestMessage(messageCountBeforeResend);
        Assert.Equal(EmailVerificationTemplates.ResendSubject, resendMessage.Subject);
        Assert.Contains("your new verification link is here", resendMessage.Text.ToLowerInvariant());
        var secondToken = ExtractToken(resendMessage.Text);
        Assert.NotEqual(firstToken, secondToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var tokens = await db.EmailVerificationTokens
            .Where(token => token.UserId == auth!.UserId)
            .OrderBy(token => token.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Null(tokens[1].RevokedAt);
        Assert.Null(tokens[1].ConsumedAt);
        Assert.Equal(Hash(secondToken), tokens[1].TokenHash);

        var oldLink = await client.PostAsJsonAsync(
            "/api/v1/auth/email/verify",
            new { token = firstToken });
        Assert.Equal(HttpStatusCode.BadRequest, oldLink.StatusCode);
        Assert.Equal("VERIFICATION_TOKEN_REVOKED", await GetErrorCodeAsync(oldLink));

        var newLink = await client.PostAsJsonAsync(
            "/api/v1/auth/email/verify",
            new { token = secondToken });
        Assert.Equal(HttpStatusCode.OK, newLink.StatusCode);
    }

    [Fact]
    public async Task Resend_is_non_enumerable_and_does_not_email_verified_or_unknown_accounts()
    {
        await _factory.EnsureSeededAsync();
        var verifiedEmail = NewEmail();
        var before = _factory.VerificationSender.Messages.Count;
        var (_, auth, _) = await RegisterAsync(verifiedEmail, "Verified Citizen");
        await VerifyLatestTokenAsync(auth!.UserId, before);
        var messageCount = _factory.VerificationSender.Messages.Count;

        using var client = _factory.CreateClient();
        var known = await client.PostAsJsonAsync(
            "/api/v1/auth/email/resend",
            new { email = verifiedEmail });
        var unknown = await client.PostAsJsonAsync(
            "/api/v1/auth/email/resend",
            new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Equal(messageCount, _factory.VerificationSender.Messages.Count);

        var activeTokens = await GetActiveTokenCountAsync(auth.UserId);
        Assert.Equal(0, activeTokens);
    }

    [Fact]
    public async Task Resend_cooldown_suppresses_new_email_without_revealing_state()
    {
        using var factory = new CmsWebApplicationFactory { VerificationCooldownSeconds = 60 };
        await factory.EnsureSeededAsync();
        var email = NewEmail();
        var before = factory.VerificationSender.Messages.Count;
        var (_, auth, _) = await RegisterWithFactoryAsync(factory, email, "Cooldown Citizen");
        Assert.NotNull(auth);

        var original = await GetActiveTokenAsync(factory, auth!.UserId);
        Assert.NotNull(original);
        var countBefore = factory.VerificationSender.Messages.Count;

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/email/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(countBefore, factory.VerificationSender.Messages.Count);
        var current = await GetActiveTokenAsync(factory, auth.UserId);
        Assert.Equal(original.Id, current!.Id);
        Assert.Equal(before + 1, factory.VerificationSender.Messages.Count);
    }

    [Fact]
    public async Task Verification_endpoints_enforce_rate_limit_policy()
    {
        using var factory = new CmsWebApplicationFactory { VerificationRateLimitPermitLimit = 1 };
        await factory.EnsureSeededAsync();
        var email = NewEmail();
        await RegisterWithFactoryAsync(factory, email, "Limited Citizen");

        using var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/v1/auth/email/resend", new { email });
        var second = await client.PostAsJsonAsync("/api/v1/auth/email/resend", new { email });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("RATE_LIMITED", await GetErrorCodeAsync(second));
    }

    [Fact]
    public async Task Provider_failure_during_resend_returns_503_without_false_delivery_claim()
    {
        await _factory.EnsureSeededAsync();
        var email = NewEmail();
        await RegisterAsync(email, "Provider Failure Citizen");
        _factory.VerificationSender.FailNext = true;

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/email/resend", new { email });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("EMAIL_DELIVERY_FAILED", await GetErrorCodeAsync(response));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.EmailHash == HashEmail(email));
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task Registration_still_succeeds_when_provider_fails_and_resend_can_recover()
    {
        await _factory.EnsureSeededAsync();
        var email = NewEmail();
        var before = _factory.VerificationSender.Messages.Count;
        _factory.VerificationSender.FailNext = true;

        var (_, auth, _) = await RegisterAsync(email, "Recover Citizen");
        Assert.NotNull(auth);
        Assert.Equal(before, _factory.VerificationSender.Messages.Count);

        using var client = _factory.CreateClient();
        var resend = await client.PostAsJsonAsync("/api/v1/auth/email/resend", new { email });
        Assert.Equal(HttpStatusCode.Accepted, resend.StatusCode);
        Assert.Equal(before + 1, _factory.VerificationSender.Messages.Count);

        var rawToken = ExtractToken(LatestMessage(before).Text);
        var verify = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token = rawToken });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
    }

    private async Task<(HttpResponseMessage Response, AuthResponseDto? Auth, string Body)> RegisterAsync(
        string email,
        string fullName) => await RegisterWithFactoryAsync(_factory, email, fullName);

    private static async Task<(HttpResponseMessage Response, AuthResponseDto? Auth, string Body)>
        RegisterWithFactoryAsync(CmsWebApplicationFactory factory, string email, string fullName)
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName,
            email,
            password = TestSecrets.Password
        });

        var body = await response.Content.ReadAsStringAsync();
        var auth = string.IsNullOrWhiteSpace(body)
            ? null
            : JsonSerializer.Deserialize<AuthResponseDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return (response, auth, body);
    }

    private async Task VerifyLatestTokenAsync(Guid userId, int messageCountBeforeRegistration)
    {
        var token = ExtractToken(_factory.VerificationSender.Messages
            .Skip(messageCountBeforeRegistration)
            .First()
            .Text);

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == userId);
        Assert.True(user.IsEmailVerified);
    }

    private VerificationEmailMessage LatestMessage(int index) =>
        _factory.VerificationSender.Messages.Skip(index).First();

    private async Task<int> GetActiveTokenCountAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        return await db.EmailVerificationTokens.CountAsync(token =>
            token.UserId == userId && token.ConsumedAt == null && token.RevokedAt == null);
    }

    private static async Task<EmailVerificationToken?> GetActiveTokenAsync(
        CmsWebApplicationFactory factory,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        return await db.EmailVerificationTokens.FirstOrDefaultAsync(token =>
            token.UserId == userId && token.ConsumedAt == null && token.RevokedAt == null);
    }

    private async Task MutateActiveTokenAsync(Guid userId, Action<EmailVerificationToken> mutate)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var token = await db.EmailVerificationTokens.FirstAsync(candidate =>
            candidate.UserId == userId && candidate.ConsumedAt == null && candidate.RevokedAt == null);

        mutate(token);
        await db.SaveChangesAsync();
    }

    private static string ExtractToken(string message) =>
        Regex.Match(message, @"[?&]token=([^&\s]+)").Groups[1].Value;

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private string HashEmail(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
        return emailProtection.HashEmail(email);
    }

    private static async Task<string> GetErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }

    private static object ValidRequest() => new
    {
        category = "Food",
        urgency = "High",
        description = "Family needs emergency food assistance.",
        lat = 9.05,
        lng = 7.49,
        householdSize = 4
    };

    private static string NewEmail() => $"verify-{Guid.NewGuid():N}@tests.local";
}
