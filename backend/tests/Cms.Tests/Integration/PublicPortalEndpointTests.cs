using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Application.DTOs.Donations;
using Cms.Domain.Constants;
using Cms.Domain.Enums;
using Xunit;

namespace Cms.Tests.Integration;

/// <summary>
/// Covers the public registration allowlist, self-service enrolment and the
/// caller-scoped donor portal endpoints.
/// </summary>
public sealed class PublicPortalEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public PublicPortalEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("volunteer", RoleNames.Volunteer)]
    [InlineData("donor", RoleNames.Donor)]
    [InlineData("citizen", RoleNames.Citizen)]
    public async Task Register_with_public_purpose_assigns_only_safe_roles(
        string purpose,
        string expectedRole)
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = $"Portal {purpose}",
            email = $"portal.{purpose}.{Guid.NewGuid():N}@tests.local",
            phoneNumber = "0771234567",
            password = TestSecrets.Password,
            accountPurpose = purpose
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(auth);
        Assert.Contains(RoleNames.Citizen, auth!.Roles);
        Assert.Contains(expectedRole, auth.Roles);
        Assert.DoesNotContain(RoleNames.SystemAdmin, auth.Roles);
        Assert.DoesNotContain(RoleNames.EmergencyCoordinator, auth.Roles);
        Assert.DoesNotContain(RoleNames.VolunteerCoordinator, auth.Roles);
        Assert.DoesNotContain(RoleNames.DonorOfficer, auth.Roles);
        Assert.DoesNotContain(RoleNames.CaseManager, auth.Roles);
        Assert.DoesNotContain(RoleNames.LogisticsManager, auth.Roles);
        Assert.DoesNotContain(RoleNames.PartnerOfficer, auth.Roles);
        Assert.DoesNotContain(RoleNames.ShelterAdmin, auth.Roles);
        Assert.All(auth.Roles, role => Assert.Contains(role, RoleNames.PublicSelfRegistrationRoles));
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("DonorOfficer")]
    [InlineData("VolunteerCoordinator")]
    [InlineData("not-a-role")]
    public async Task Register_rejects_privileged_or_unknown_purpose(string purpose)
    {
        await _factory.EnsureSeededAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Invalid Purpose",
            email = $"invalid.{Guid.NewGuid():N}@tests.local",
            phoneNumber = "0771234567",
            password = TestSecrets.Password,
            accountPurpose = purpose
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Donor_registration_creates_a_linked_donor_profile()
    {
        await _factory.EnsureSeededAsync();
        var email = $"linked.donor.{Guid.NewGuid():N}@tests.local";
        using var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Linked Donor",
            email,
            phoneNumber = "0771234567",
            password = TestSecrets.Password,
            accountPurpose = "donor"
        });
        register.EnsureSuccessStatusCode();

        using var donorClient = await _factory.CreateAuthenticatedClientAsync(email);
        var response = await donorClient.GetAsync("/api/v1/portal/donor/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<DonorDto>(JsonOptions);
        Assert.NotNull(profile);
        Assert.Equal("Linked Donor", profile!.Name);
        Assert.Equal(DonorType.Individual, profile.DonorType);
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("DonorOfficer")]
    [InlineData("Staff")]
    public async Task Enroll_rejects_privileged_roles(string purpose)
    {
        await _factory.EnsureSeededAsync();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var response = await citizen.PostAsJsonAsync(
            "/api/v1/portal/enroll",
            new { purpose });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var me = await citizen.GetFromJsonAsync<CurrentUserDto>("/api/v1/auth/me");
        Assert.NotNull(me);
        Assert.DoesNotContain(RoleNames.SystemAdmin, me!.Roles);
        Assert.DoesNotContain(RoleNames.DonorOfficer, me.Roles);
    }

    [Fact]
    public async Task Enroll_adds_only_the_requested_public_role()
    {
        await _factory.EnsureSeededAsync();
        var email = $"enroll.volunteer.{Guid.NewGuid():N}@tests.local";
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Enrolling Volunteer",
            email,
            phoneNumber = "0771234567",
            password = TestSecrets.Password
        });

        using var volunteer = await _factory.CreateAuthenticatedClientAsync(email);
        var response = await volunteer.PostAsJsonAsync(
            "/api/v1/portal/enroll",
            new { purpose = "volunteer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.NotNull(refreshed);
        Assert.Equal(
            new[] { RoleNames.Citizen, RoleNames.Volunteer },
            refreshed!.Roles.Order().ToArray());

        // The original token was issued before enrolment and cannot contain the
        // new role claim. Use the refreshed token returned by enrolment.
        volunteer.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);

        var status = await volunteer.GetAsync("/api/v1/portal/volunteer/status");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Fact]
    public async Task Donor_portal_reads_and_writes_only_the_callers_data()
    {
        await _factory.EnsureSeededAsync();
        var firstEmail = $"donor.a.{Guid.NewGuid():N}@tests.local";
        var secondEmail = $"donor.b.{Guid.NewGuid():N}@tests.local";

        using var registration = _factory.CreateClient();
        foreach (var email in new[] { firstEmail, secondEmail })
        {
            var created = await registration.PostAsJsonAsync("/api/v1/auth/register", new
            {
                fullName = email.Split('.')[0],
                email,
                phoneNumber = "0771234567",
                password = TestSecrets.Password,
                accountPurpose = "donor"
            });
            created.EnsureSuccessStatusCode();
        }

        using var firstDonor = await _factory.CreateAuthenticatedClientAsync(firstEmail);
        using var secondDonor = await _factory.CreateAuthenticatedClientAsync(secondEmail);

        var firstProfile = await firstDonor.GetFromJsonAsync<DonorDto>(
            "/api/v1/portal/donor/profile", JsonOptions);
        var secondProfile = await secondDonor.GetFromJsonAsync<DonorDto>(
            "/api/v1/portal/donor/profile", JsonOptions);
        Assert.NotNull(firstProfile);
        Assert.NotNull(secondProfile);
        Assert.NotEqual(firstProfile!.Id, secondProfile!.Id);

        var record = await firstDonor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonorId = secondProfile.Id, // must be ignored by the server
                DonationType = DonationType.Monetary,
                Amount = 25m,
                Currency = "USD",
                Notes = "Portal test donation"
            });
        Assert.Equal(HttpStatusCode.OK, record.StatusCode);
        var donation = await record.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(donation);
        Assert.Equal(firstProfile.Id, donation!.DonorId);

        var mine = await firstDonor.GetFromJsonAsync<PagedResultDto<DonationDto>>(
            "/api/v1/portal/donor/donations", JsonOptions);
        Assert.NotNull(mine);
        Assert.Contains(mine!.Items, item => item.Id == donation.Id);

        var other = await secondDonor.GetFromJsonAsync<PagedResultDto<DonationDto>>(
            $"/api/v1/portal/donor/donations?donorId={firstProfile.Id}", JsonOptions);
        Assert.NotNull(other);
        Assert.DoesNotContain(other!.Items, item => item.Id == donation.Id);
        Assert.Equal(0, other.TotalCount);
    }

    [Fact]
    public async Task Portal_donor_routes_reject_callers_without_the_donor_role()
    {
        await _factory.EnsureSeededAsync();
        using var anonymous = _factory.CreateClient();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/v1/portal/donor/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await citizen.GetAsync("/api/v1/portal/donor/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await admin.GetAsync("/api/v1/portal/donor/campaigns")).StatusCode);
    }
}
