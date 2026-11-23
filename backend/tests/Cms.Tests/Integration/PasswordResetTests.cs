using System.Net;
using System.Net.Http.Json;
using Cms.Application.DTOs.Authentication;
using Cms.Infrastructure.Data;
using Cms.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cms.Tests.Integration;

public sealed class PasswordResetTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public PasswordResetTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_returns_generic_success_and_does_not_expose_token()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = CmsWebApplicationFactory.CitizenEmail
        });
        var unknown = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = "nobody@example.test"
        });

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);

        var knownBody = await known.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
        var unknownBody = await unknown.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
        var knownRaw = await known.Content.ReadAsStringAsync();

        Assert.NotNull(knownBody);
        Assert.NotNull(unknownBody);
        Assert.Equal(knownBody!.Message, unknownBody!.Message);
        Assert.False(knownBody.EmailDeliveryConfigured);
        Assert.DoesNotContain("token", knownRaw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reset-password?", knownRaw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForgotPassword_stores_only_hashed_single_use_token()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new
        {
            email = CmsWebApplicationFactory.OfficerEmail
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
        var emailHash = emailProtection.HashEmail(CmsWebApplicationFactory.OfficerEmail);
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.EmailHash == emailHash);

        var tokens = await db.PasswordResetTokens.AsNoTracking()
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        Assert.NotEmpty(tokens);
        var latest = tokens.First();
        Assert.Equal(64, latest.TokenHash.Length);
        Assert.Matches("^[a-fA-F0-9]{64}$", latest.TokenHash);
        Assert.Null(latest.UsedAt);
        Assert.True(latest.ExpiresAt > DateTime.UtcNow.AddMinutes(20));
    }

    [Fact]
    public async Task ResetPassword_with_valid_token_updates_password_and_invalidates_token()
    {
        await _factory.EnsureSeededAsync();
        const string email = "reset.flow@tests.local";
        await RegisterTestUserAsync(email);

        using var client = _factory.CreateClient();
        var forgot = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);

        var (userId, plainToken) = await CreateKnownResetTokenAsync(email);

        var reset = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = plainToken,
            newPassword = "NewPassword123!",
            confirmPassword = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = TestSecrets.Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var newLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = plainToken,
            newPassword = "AnotherPassword123!",
            confirmPassword = "AnotherPassword123!"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reuse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var tokenRow = await db.PasswordResetTokens.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstAsync();
        Assert.NotNull(tokenRow.UsedAt);
    }

    [Fact]
    public async Task ResetPassword_rejects_expired_or_invalid_token()
    {
        await _factory.EnsureSeededAsync();
        const string email = "reset.expired@tests.local";
        await RegisterTestUserAsync(email);
        var (_, plainToken) = await CreateKnownResetTokenAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
            var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
            var emailHash = emailProtection.HashEmail(email);
            var user = await db.Users.SingleAsync(u => u.EmailHash == emailHash);
            var tokenRow = await db.PasswordResetTokens
                .Where(t => t.UserId == user.Id && t.UsedAt == null)
                .OrderByDescending(t => t.CreatedAt)
                .FirstAsync();
            tokenRow.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var expired = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = plainToken,
            newPassword = "NewPassword123!",
            confirmPassword = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);

        var invalid = await client.PostAsJsonAsync("/api/v1/auth/reset-password", new
        {
            token = "not-a-real-token",
            newPassword = "NewPassword123!",
            confirmPassword = "NewPassword123!"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }

    private async Task RegisterTestUserAsync(string email)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Reset Test User",
            email,
            password = TestSecrets.Password
        });

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<(Guid UserId, string PlainToken)> CreateKnownResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var emailProtection = scope.ServiceProvider.GetRequiredService<EmailProtectionService>();
        var emailHash = emailProtection.HashEmail(email);
        var user = await db.Users.SingleAsync(u => u.EmailHash == emailHash);

        var plainToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(plainToken));
        var tokenHash = Convert.ToHexString(hash);

        db.PasswordResetTokens.Add(new Cms.Domain.Entities.PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return (user.Id, plainToken);
    }
}
