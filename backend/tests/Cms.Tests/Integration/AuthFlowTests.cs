using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Domain.Constants;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Cms.Tests.Integration;

public sealed class AuthFlowTests : IClassFixture<CmsWebApplicationFactory>
{
    private readonly CmsWebApplicationFactory _factory;

    public AuthFlowTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_creates_citizen_and_hashes_email()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "New Citizen",
            email = "new.citizen@tests.local",
            phoneNumber = "0771234567",
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);
        Assert.Single(auth!.Roles);
        Assert.Equal(RoleNames.Citizen, auth.Roles.Single());
        Assert.Equal("new.citizen@tests.local", auth.Email);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Single(token.Claims, claim => claim.Type == "role");
        Assert.Equal("false", token.Claims.Single(claim => claim.Type == "email_verified").Value);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "orgId");
    }

    [Fact]
    public async Task Unverified_account_can_login_refresh_and_read_status()
    {
        await _factory.EnsureSeededAsync();
        var email = $"unverified.status.{Guid.NewGuid():N}@tests.local";
        using var registration = _factory.CreateClient();
        var register = await registration.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Unverified Status",
            email,
            phoneNumber = "0771234567",
            password = TestSecrets.Password
        });
        register.EnsureSuccessStatusCode();
        var registered = await register.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(registered);
        Assert.False(registered!.IsEmailVerified);

        var login = await _factory.LoginAsync(email);
        Assert.False(login.IsEmailVerified);

        using var refreshClient = _factory.CreateClient();
        var refresh = await refreshClient.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        using var meClient = await _factory.CreateAuthenticatedClientAsync(email);
        var me = await meClient.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.False(me!.IsEmailVerified);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_case_insensitively()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Duplicate Citizen",
            email = "CITIZEN@TESTS.LOCAL",
            phoneNumber = "0771234567",
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("EMAIL_ALREADY_EXISTS", await GetErrorCodeAsync(response));
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_inactive_user()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = CmsWebApplicationFactory.CitizenEmail,
            password = "WrongPassword1!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var inactive = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = CmsWebApplicationFactory.InactiveEmail,
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.Unauthorized, inactive.StatusCode);
    }

    [Fact]
    public async Task Login_returns_separate_role_claims_and_org_claim()
    {
        var auth = await _factory.LoginAsync(CmsWebApplicationFactory.OfficerEmail);

        Assert.Equal(2, auth.Roles.Count);
        Assert.Contains(RoleNames.PartnerOfficer, auth.Roles);
        Assert.Contains(RoleNames.CaseManager, auth.Roles);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        Assert.Equal(2, token.Claims.Count(claim => claim.Type == "role"));
        Assert.Single(token.Claims, claim => claim.Type == "orgId");
        Assert.Single(token.Claims, claim => claim.Type == "jti");
    }

    [Fact]
    public async Task Refresh_rotates_token_and_detects_reuse()
    {
        await _factory.EnsureSeededAsync();
        var first = await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail);
        using var client = _factory.CreateClient();

        var rotatedResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = first.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(rotated);
        Assert.NotEqual(first.RefreshToken, rotated!.RefreshToken);

        var reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = first.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var revokedChain = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = rotated.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, revokedChain.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var auth = await _factory.LoginAsync(CmsWebApplicationFactory.CitizenEmail);
        using var client = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new
        {
            refreshToken = auth.RefreshToken
        });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var anonymous = _factory.CreateClient();
        var refresh = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = auth.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_returns_current_user_with_decrypted_email()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.OfficerEmail);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var current = await response.Content.ReadFromJsonAsync<CurrentUserDto>();
        Assert.NotNull(current);
        Assert.Equal(CmsWebApplicationFactory.OfficerEmail, current!.Email);
        Assert.Contains(RoleNames.PartnerOfficer, current.Roles);
        Assert.Contains(RoleNames.CaseManager, current.Roles);
    }

    [Theory]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("GET", "/api/v1/auth/role-check")]
    [InlineData("POST", "/api/v1/auth/logout")]
    public async Task Protected_auth_endpoints_reject_anonymous_callers(string method, string path)
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { refreshToken = "not-a-real-token" })
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Two_role_user_passes_role_policy_and_single_role_users_fail()
    {
        using var officer = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.OfficerEmail);
        using var citizen = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.CitizenEmail);
        using var admin = await _factory.CreateAuthenticatedClientAsync(CmsWebApplicationFactory.AdminEmail);

        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync("/api/v1/auth/role-check")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await citizen.GetAsync("/api/v1/auth/role-check")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/auth/role-check")).StatusCode);
    }

    [Fact]
    public async Task Expired_access_token_is_rejected()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var token = new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "Cms.Api",
            Audience = "Cms.Client",
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecrets.JwtSecret)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["role"] = RoleNames.Citizen
            }
        });

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                new JwtSecurityTokenHandler().WriteToken(token));

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_missing_phone_number()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "No Phone",
            email = $"nophone.{Guid.NewGuid():N}@tests.local",
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_invalid_phone_number()
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Bad Phone",
            email = $"badphone.{Guid.NewGuid():N}@tests.local",
            phoneNumber = "12345",
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("0771234567", "+94771234567")]
    [InlineData("+94771234567", "+94771234567")]
    [InlineData("077-123 4567", "+94771234567")]
    [InlineData("+94 77 123 4567", "+94771234567")]
    public async Task Register_normalizes_valid_sri_lankan_phone_numbers(string input, string expected)
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();
        var email = $"phone.{Guid.NewGuid():N}@tests.local";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Phone User",
            email,
            phoneNumber = input,
            password = TestSecrets.Password
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CmsDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == auth!.UserId);
        Assert.Equal(expected, user.PhoneNumber);
    }

    private static async Task<string?> GetErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }
}
