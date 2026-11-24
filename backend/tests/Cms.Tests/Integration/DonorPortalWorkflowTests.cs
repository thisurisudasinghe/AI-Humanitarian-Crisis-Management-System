using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Application.DTOs.Donations;
using Cms.Domain.Enums;
using Xunit;

namespace Cms.Tests.Integration;

/// <summary>
/// Regression coverage for the safe donor-facing workflow additions:
/// structured multi-item supplies, custom values, location, campaign intent,
/// client idempotency and input validation.
/// </summary>
public sealed class DonorPortalWorkflowTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public DonorPortalWorkflowTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateDonorAsync()
    {
        await _factory.EnsureSeededAsync();
        var email = $"donor.workflow.{Guid.NewGuid():N}@tests.local";

        using var registration = _factory.CreateClient();
        var created = await registration.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Workflow Donor",
            email,
            phoneNumber = "0771234567",
            password = TestSecrets.Password,
            accountPurpose = "donor"
        });
        created.EnsureSuccessStatusCode();
        var auth = await created.Content.ReadFromJsonAsync<AuthResponseDto>(JsonOptions);
        Assert.NotNull(auth);

        return await _factory.CreateAuthenticatedClientAsync(email);
    }

    [Fact]
    public async Task Multi_item_donation_keeps_custom_items_units_and_location()
    {
        using var donor = await CreateDonorAsync();

        var response = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Material,
                Currency = "LKR",
                Category = "Other",
                LocationText = "Colombo 07 collection point",
                Latitude = 6.9325,
                Longitude = 79.86,
                DeliveryMethod = "Drop-off",
                IdempotencyKey = $"items-{Guid.NewGuid():N}",
                Items =
                [
                    new CreateDonationItemDto
                    {
                        Name = "Rice",
                        Category = "Food",
                        Quantity = 10,
                        Unit = "Kg"
                    },
                    new CreateDonationItemDto
                    {
                        Name = "Rechargeable emergency lights",
                        Category = "Other",
                        Quantity = 3,
                        Unit = "Emergency light sets",
                        Condition = "New"
                    }
                ]
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var donation = await response.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(donation);
        Assert.Equal(2, donation!.Items.Count);
        Assert.Contains(donation.Items, item =>
            item.Name == "Rice" && item.Quantity == 10 && item.Unit == "Kg");
        Assert.Contains(donation.Items, item =>
            item.Name == "Rechargeable emergency lights"
            && item.Unit == "Emergency light sets"
            && item.Condition == "New");
        Assert.Equal(13, donation.Quantity);
        Assert.Equal("Colombo 07 collection point", donation.LocationText);
        Assert.NotNull(donation.Latitude);
        Assert.NotNull(donation.Longitude);
        Assert.InRange(donation.Latitude.Value, 6.9324, 6.9326);
        Assert.InRange(donation.Longitude.Value, 79.8599, 79.8601);
        Assert.Equal(DonationStatus.Received, donation.Status);
        Assert.False(donation.IsSuspicious);

        var history = await donor.GetFromJsonAsync<PagedResultDto<DonationDto>>(
            "/api/v1/portal/donor/donations", JsonOptions);
        Assert.NotNull(history);
        var saved = Assert.Single(history!.Items, item => item.Id == donation.Id);
        Assert.Equal(2, saved.Items.Count);
    }

    [Fact]
    public async Task Replaying_an_idempotency_key_returns_the_original_donation()
    {
        using var donor = await CreateDonorAsync();
        var key = $"replay-{Guid.NewGuid():N}";

        var first = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Monetary,
                Amount = 25m,
                Currency = "LKR",
                IdempotencyKey = key,
                Notes = "Original"
            });
        first.EnsureSuccessStatusCode();
        var original = await first.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);

        var replay = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Monetary,
                Amount = 999m,
                Currency = "LKR",
                IdempotencyKey = key,
                Notes = "Duplicate"
            });
        replay.EnsureSuccessStatusCode();
        var repeated = await replay.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);

        Assert.NotNull(original);
        Assert.NotNull(repeated);
        Assert.Equal(original!.Id, repeated!.Id);
        Assert.Equal(original.Reference, repeated.Reference);
        Assert.Equal(25m, repeated.Amount);

        var history = await donor.GetFromJsonAsync<PagedResultDto<DonationDto>>(
            "/api/v1/portal/donor/donations", JsonOptions);
        Assert.NotNull(history);
        Assert.Single(history!.Items);
    }

    [Fact]
    public async Task Invalid_coordinates_and_campaigns_are_rejected()
    {
        using var donor = await CreateDonorAsync();

        var coordinates = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Material,
                Currency = "LKR",
                ItemDescription = "Blankets",
                Quantity = 2,
                Latitude = 95,
                Longitude = 79
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, coordinates.StatusCode);

        var campaign = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Monetary,
                Amount = 10m,
                Currency = "LKR",
                CampaignId = Guid.NewGuid()
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, campaign.StatusCode);
    }

    [Fact]
    public async Task Selected_campaign_is_recorded_without_donor_allocation()
    {
        await _factory.EnsureSeededAsync();
        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var campaignResponse = await officer.PostAsJsonAsync(
            "/api/v1/donations/campaigns",
            new CreateCampaignRequestDto
            {
                Name = $"Donor portal campaign {Guid.NewGuid():N}",
                Description = "Test campaign for donor portal regression coverage.",
                GoalAmount = 1000m,
                Currency = "LKR",
                StartsAt = DateTime.UtcNow.AddDays(-1),
                EndsAt = DateTime.UtcNow.AddDays(30)
            });
        campaignResponse.EnsureSuccessStatusCode();
        var campaign = await campaignResponse.Content.ReadFromJsonAsync<CampaignDto>(JsonOptions);
        Assert.NotNull(campaign);

        var activate = await officer.PatchAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign!.Id}",
            new UpdateCampaignRequestDto { Status = CampaignStatus.Active });
        activate.EnsureSuccessStatusCode();

        using var donor = await CreateDonorAsync();
        var response = await donor.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Monetary,
                Amount = 50m,
                Currency = "LKR",
                CampaignId = campaign!.Id,
                Notes = "Campaign support"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var donation = await response.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(donation);
        Assert.Equal(campaign.Id, donation!.CampaignId);
        Assert.Equal(DonationStatus.Received, donation.Status);
        Assert.Equal(0m, donation.AllocatedAmount);
    }

    [Fact]
    public async Task Donation_detail_is_scoped_to_the_signed_in_donor()
    {
        using var owner = await CreateDonorAsync();
        using var otherDonor = await CreateDonorAsync();

        var created = await owner.PostAsJsonAsync(
            "/api/v1/portal/donor/donations",
            new CreateDonationRequestDto
            {
                DonationType = DonationType.Monetary,
                Amount = 40m,
                Currency = "LKR",
                IdempotencyKey = $"detail-{Guid.NewGuid():N}"
            });
        created.EnsureSuccessStatusCode();
        var donation = await created.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(donation);

        var ownDetail = await owner.GetAsync(
            $"/api/v1/portal/donor/donations/{donation!.Id}");
        ownDetail.EnsureSuccessStatusCode();

        var otherDetail = await otherDonor.GetAsync(
            $"/api/v1/portal/donor/donations/{donation.Id}");
        Assert.Equal(HttpStatusCode.NotFound, otherDetail.StatusCode);
    }
}
