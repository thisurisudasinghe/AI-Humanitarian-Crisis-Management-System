using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.DTOs.Donations;
using Cms.Application.DTOs.Kpi;
using Cms.Domain.Enums;

namespace Cms.Tests.Integration;

public sealed class KpiEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public KpiEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kpi_report_includes_identity_audit_and_donation_sections()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var currency = UniqueCurrency();
        var donor = await CreateDonorAsync(admin, $"KPI Donor {Guid.NewGuid():N}");
        var campaign = await CreateActiveCampaignAsync(admin, goalAmount: 1000m, currency: currency);
        var donation = await CreateDonationAsync(admin, donor.Id, amount: 250m, currency: currency);

        var allocationResponse = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = campaign.Id,
                Amount = 100m
            });
        Assert.Equal(HttpStatusCode.OK, allocationResponse.StatusCode);

        var report = await GetReportAsync(admin, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(2));

        Assert.True(report.IsPlaceholder);
        Assert.Contains("identity", report.AvailableSections);
        Assert.Contains("donations", report.AvailableSections);
        Assert.Contains("cases", report.PendingSections);
        Assert.Contains("notifications", report.PendingSections);

        Assert.True(report.TotalUserCount >= 4);
        Assert.True(report.ActiveUserCount >= 3);
        Assert.True(report.NewUserCount >= 4);

        var adminRole = Assert.Single(
            report.UsersByRole,
            item => item.Role == "SystemAdmin");
        Assert.True(adminRole.Count >= 1);

        Assert.True(report.AuditEventCount > 0);
        Assert.True(report.ActiveCampaignCount >= 1);

        var totals = Assert.Single(
            report.DonationTotals,
            item => item.Currency == currency);
        Assert.Equal(250m, totals.MonetaryTotal);
        Assert.Equal(0m, totals.MaterialEstimatedTotal);
        Assert.Equal(100m, totals.AllocatedTotal);
        Assert.Equal(0m, totals.SpentTotal);
        Assert.Equal(1, totals.DonationCount);
        Assert.Equal(1, totals.DonorCount);
        Assert.Equal(0, totals.SuspiciousCount);
        Assert.Equal(0, report.SuspiciousDonationCount);
        Assert.Equal(0, report.SuspiciousAllocationCount);
    }

    [Fact]
    public async Task Kpi_report_limits_period_sections_to_the_requested_window()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var report = await GetReportAsync(
            admin,
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0, report.NewUserCount);
        Assert.Equal(0, report.AuditEventCount);
        Assert.Empty(report.DonationTotals);
        Assert.Equal(0, report.SuspiciousDonationCount);
        Assert.Equal(0, report.SuspiciousAllocationCount);

        // Organisation-wide totals are not date-bounded.
        Assert.True(report.TotalUserCount >= 4);
        Assert.True(report.ActiveUserCount >= 3);
        Assert.NotEmpty(report.UsersByRole);
    }

    [Fact]
    public async Task Kpi_report_keeps_cross_module_sections_pending()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var report = await GetReportAsync(
            admin,
            DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(1));

        Assert.Null(report.AverageResponseTimeHours);
        Assert.Null(report.ResourceUtilisationPercent);
        Assert.Null(report.ReservedResources);
        Assert.Null(report.DispatchedResources);
        Assert.Null(report.ShelterOccupancyPercent);
        Assert.Null(report.SheltersAtCapacity);
        Assert.Null(report.AiOverrideRatePercent);
        Assert.Null(report.FailedNotifications);
        Assert.Null(report.DeadLetterCount);
        Assert.Equal(0, report.UnresolvedRequestCount);
        Assert.Empty(report.RequestsByArea);
        Assert.Empty(report.SheltersByArea);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<KpiReportDto> GetReportAsync(
        HttpClient client,
        DateTime from,
        DateTime to)
    {
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}" +
                    $"&to={Uri.EscapeDataString(to.ToString("O"))}";

        var response = await client.GetAsync($"/api/v1/kpis?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await response.Content.ReadFromJsonAsync<KpiReportDto>(JsonOptions);
        Assert.NotNull(report);

        return report!;
    }

    private static async Task<DonorDto> CreateDonorAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/donations/donors",
            new CreateDonorRequestDto
            {
                Name = name,
                DonorType = DonorType.Individual
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<DonorDto>(JsonOptions)
            ?? throw new InvalidOperationException("Donor response was empty.");
    }

    private static async Task<CampaignDto> CreateActiveCampaignAsync(
        HttpClient client,
        decimal goalAmount,
        string currency)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/donations/campaigns",
            new CreateCampaignRequestDto
            {
                Name = $"KPI Campaign {Guid.NewGuid():N}",
                GoalAmount = goalAmount,
                Currency = currency,
                StartsAt = DateTime.UtcNow.AddDays(-1)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var campaign = await response.Content.ReadFromJsonAsync<CampaignDto>(JsonOptions)
            ?? throw new InvalidOperationException("Campaign response was empty.");

        var activateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}",
            new UpdateCampaignRequestDto { Status = CampaignStatus.Active });

        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        return await activateResponse.Content.ReadFromJsonAsync<CampaignDto>(JsonOptions)
            ?? throw new InvalidOperationException("Campaign response was empty.");
    }

    private static async Task<DonationDto> CreateDonationAsync(
        HttpClient client,
        Guid donorId,
        decimal amount,
        string currency)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donorId,
                DonationType = DonationType.Monetary,
                Amount = amount,
                Currency = currency,
                ReceivedAt = DateTime.UtcNow
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<DonationDto>(JsonOptions)
            ?? throw new InvalidOperationException("Donation response was empty.");
    }

    /// <summary>Builds a three-letter currency code so KPI totals stay isolated.</summary>
    private static string UniqueCurrency()
    {
        const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var random = Random.Shared;

        return new string(
        [
            'X',
            letters[random.Next(letters.Length)],
            letters[random.Next(letters.Length)]
        ]);
    }
}
