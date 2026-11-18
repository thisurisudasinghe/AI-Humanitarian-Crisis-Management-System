using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.Common;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Audit;
using Cms.Application.DTOs.Donations;
using Cms.Domain.Enums;

namespace Cms.Tests.Integration;

public sealed class DonationEndpointTests : IClassFixture<CmsWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly CmsWebApplicationFactory _factory;

    public DonationEndpointTests(CmsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // -----------------------------------------------------------------------
    // Authentication and authorisation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/donations")]
    [InlineData("/api/v1/donations/donors")]
    [InlineData("/api/v1/donations/campaigns")]
    [InlineData("/api/v1/donations/report?from=2026-01-01&to=2026-12-31")]
    public async Task Donation_reads_reject_anonymous_callers(string path)
    {
        await _factory.EnsureSeededAsync();
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Donation_reads_reject_citizens()
    {
        await _factory.EnsureSeededAsync();
        using var citizen = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.CitizenEmail);

        var response = await citizen.GetAsync("/api/v1/donations/donors");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/v1/donations/donors")]
    [InlineData("POST", "/api/v1/donations/campaigns")]
    [InlineData("POST", "/api/v1/donations")]
    [InlineData("PATCH", "/api/v1/donations/donors/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/v1/donations/00000000-0000-0000-0000-000000000001/allocations")]
    public async Task Donation_writes_reject_read_only_viewers(string method, string path)
    {
        await _factory.EnsureSeededAsync();
        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        };

        var response = await officer.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Donor, campaign, donation and allocation lifecycle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Donor_officer_can_run_the_full_donation_lifecycle()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donorName = $"Lifecycle Donor {Guid.NewGuid():N}";
        var donor = await CreateDonorAsync(admin, donorName);

        var donorResponse = await admin.GetAsync($"/api/v1/donations/donors/{donor.Id}");
        Assert.Equal(HttpStatusCode.OK, donorResponse.StatusCode);
        var storedDonor = await donorResponse.Content.ReadFromJsonAsync<DonorDto>(JsonOptions);
        Assert.NotNull(storedDonor);
        Assert.Equal(donorName, storedDonor!.Name);
        Assert.True(storedDonor.IsActive);

        var campaign = await CreateActiveCampaignAsync(admin, goalAmount: 1000m, currency: "USD");
        Assert.Equal(CampaignStatus.Active, campaign.Status);

        var donation = await CreateDonationAsync(admin, donor.Id, amount: 500m, currency: "USD");
        Assert.Equal(DonationStatus.Received, donation.Status);
        Assert.False(donation.IsSuspicious);
        Assert.Equal(500m, donation.AllocatableValue);
        Assert.Equal(0m, donation.AllocatedAmount);
        Assert.Equal(500m, donation.UnallocatedAmount);

        var allocationResponse = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = campaign.Id,
                Amount = 200m,
                Note = "Emergency water"
            });

        Assert.Equal(HttpStatusCode.OK, allocationResponse.StatusCode);
        var allocated = await allocationResponse.Content
            .ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(allocated);
        Assert.Equal(200m, allocated!.AllocatedAmount);
        Assert.Equal(300m, allocated.UnallocatedAmount);
        Assert.Equal(DonationStatus.Received, allocated.Status);
        var allocation = Assert.Single(allocated.Allocations);
        Assert.Equal(campaign.Id, allocation.CampaignId);
        Assert.Equal("Lifecycle Campaign", allocation.CampaignName);
        Assert.False(allocation.IsSuspicious);

        var finalResponse = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = campaign.Id,
                Amount = 300m
            });

        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);
        var fullyAllocated = await finalResponse.Content
            .ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(fullyAllocated);
        Assert.Equal(500m, fullyAllocated!.AllocatedAmount);
        Assert.Equal(0m, fullyAllocated.UnallocatedAmount);
        Assert.Equal(DonationStatus.Allocated, fullyAllocated.Status);

        var campaignResponse = await admin.GetAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}");
        Assert.Equal(HttpStatusCode.OK, campaignResponse.StatusCode);
        var updatedCampaign = await campaignResponse.Content
            .ReadFromJsonAsync<CampaignDto>(JsonOptions);
        Assert.NotNull(updatedCampaign);
        Assert.Equal(500m, updatedCampaign!.RaisedAmount);
        Assert.Equal(500m, updatedCampaign.RemainingAmount);
        Assert.Equal(50d, updatedCampaign.ProgressPercent);

        var donorAfter = await admin.GetAsync($"/api/v1/donations/donors/{donor.Id}");
        var refreshedDonor = await donorAfter.Content.ReadFromJsonAsync<DonorDto>(JsonOptions);
        Assert.NotNull(refreshedDonor);
        Assert.Equal(1, refreshedDonor!.DonationCount);
        Assert.Equal(500m, refreshedDonor.TotalContributed);

        var auditResponse = await admin.GetAsync(
            $"/api/v1/audit-events?entityType=don.Donation&entityId={donation.Id}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var audit = await auditResponse.Content.ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!.Items, item => item.Action == AuditActions.DonationRecorded);

        var allocationAuditResponse = await admin.GetAsync(
            $"/api/v1/audit-events?action={AuditActions.DonationAllocated}&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, allocationAuditResponse.StatusCode);
        var allocationAudit = await allocationAuditResponse.Content
            .ReadFromJsonAsync<PagedResultDto<AuditEventDto>>();
        Assert.NotNull(allocationAudit);
        Assert.Contains(allocationAudit!.Items, item => item.Action == AuditActions.DonationAllocated);
    }

    [Fact]
    public async Task Donor_and_campaign_updates_are_persisted_and_validated()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donor = await CreateDonorAsync(admin, $"Update Donor {Guid.NewGuid():N}");

        var updateResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/donors/{donor.Id}",
            new UpdateDonorRequestDto
            {
                ContactEmail = "donor@example.org",
                IsAnonymous = true,
                IsActive = false
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DonorDto>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("donor@example.org", updated!.ContactEmail);
        Assert.True(updated.IsAnonymous);
        Assert.False(updated.IsActive);

        var blankName = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/donors/{donor.Id}",
            new UpdateDonorRequestDto { Name = "   " });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blankName.StatusCode);

        var inactiveDonation = await admin.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donor.Id,
                DonationType = DonationType.Monetary,
                Amount = 10m,
                Currency = "USD"
            });
        Assert.Equal(HttpStatusCode.BadRequest, inactiveDonation.StatusCode);

        var campaign = await CreateActiveCampaignAsync(admin, goalAmount: 100m, currency: "GBP");

        var badDate = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}",
            new UpdateCampaignRequestDto { EndsAt = DateTime.UtcNow.AddDays(-5) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badDate.StatusCode);

        var completeResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}",
            new UpdateCampaignRequestDto { Status = CampaignStatus.Completed });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content
            .ReadFromJsonAsync<CampaignDto>(JsonOptions);
        Assert.NotNull(completed);
        Assert.Equal(CampaignStatus.Completed, completed!.Status);

        var reopenResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}",
            new UpdateCampaignRequestDto { Status = CampaignStatus.Active });
        Assert.Equal(HttpStatusCode.BadRequest, reopenResponse.StatusCode);
    }

    // -----------------------------------------------------------------------
    // Validation rules
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Material_donations_require_an_item_and_monetary_ones_an_amount()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donor = await CreateDonorAsync(admin, $"Validation Donor {Guid.NewGuid():N}");

        var materialWithoutItem = await admin.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donor.Id,
                DonationType = DonationType.Material,
                EstimatedValue = 50m,
                Currency = "USD"
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, materialWithoutItem.StatusCode);
        using var materialProblem = JsonDocument.Parse(
            await materialWithoutItem.Content.ReadAsStringAsync());
        Assert.Equal(
            "VALIDATION_FAILED",
            materialProblem.RootElement.GetProperty("code").GetString());

        var monetaryWithoutAmount = await admin.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donor.Id,
                DonationType = DonationType.Monetary,
                Currency = "USD"
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, monetaryWithoutAmount.StatusCode);

        var futureDonation = await admin.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donor.Id,
                DonationType = DonationType.Monetary,
                Amount = 10m,
                Currency = "USD",
                ReceivedAt = DateTime.UtcNow.AddDays(2)
            });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, futureDonation.StatusCode);

        var material = await admin.PostAsJsonAsync(
            "/api/v1/donations",
            new CreateDonationRequestDto
            {
                DonorId = donor.Id,
                DonationType = DonationType.Material,
                ItemDescription = "Blankets",
                Quantity = 40,
                EstimatedValue = 250.555m,
                Currency = "usd"
            });

        Assert.Equal(HttpStatusCode.Created, material.StatusCode);
        var created = await material.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(DonationType.Material, created!.DonationType);
        Assert.Null(created.Amount);
        Assert.Equal(250.56m, created.EstimatedValue);
        Assert.Equal(250.56m, created.AllocatableValue);
        Assert.Equal("USD", created.Currency);
        Assert.StartsWith("DON-", created.Reference, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Suspicious donation rules (FR-DON-06)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Large_donations_are_flagged_and_a_human_can_clear_the_flag()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donor = await CreateDonorAsync(admin, $"Large Donor {Guid.NewGuid():N}");

        var donation = await CreateDonationAsync(
            admin,
            donor.Id,
            amount: 25_000m,
            currency: "USD");

        Assert.True(donation.IsSuspicious);
        Assert.NotNull(donation.SuspiciousReason);
        Assert.Equal(DonationStatus.Received, donation.Status);

        var clearResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/flag",
            new FlagRequestDto { IsSuspicious = false });

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(cleared);
        Assert.False(cleared!.IsSuspicious);
        Assert.Null(cleared.SuspiciousReason);

        var reflagResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/flag",
            new FlagRequestDto { IsSuspicious = true, Reason = "Manual review requested" });

        Assert.Equal(HttpStatusCode.OK, reflagResponse.StatusCode);
        var reflagged = await reflagResponse.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(reflagged);
        Assert.True(reflagged!.IsSuspicious);
        Assert.Equal("Manual review requested", reflagged.SuspiciousReason);
    }

    [Fact]
    public async Task Allocations_cannot_exceed_value_or_cross_currencies()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donor = await CreateDonorAsync(admin, $"Allocation Donor {Guid.NewGuid():N}");
        var usdCampaign = await CreateActiveCampaignAsync(admin, goalAmount: 1000m, currency: "USD");
        var gbpCampaign = await CreateActiveCampaignAsync(admin, goalAmount: 1000m, currency: "GBP");

        var donation = await CreateDonationAsync(admin, donor.Id, amount: 100m, currency: "USD");

        var tooMuch = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = usdCampaign.Id,
                Amount = 150m
            });

        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        using var tooMuchProblem = JsonDocument.Parse(
            await tooMuch.Content.ReadAsStringAsync());
        Assert.Equal(
            "ALLOCATION_EXCEEDS_VALUE",
            tooMuchProblem.RootElement.GetProperty("code").GetString());

        var wrongCurrency = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = gbpCampaign.Id,
                Amount = 10m
            });

        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrency.StatusCode);
        using var currencyProblem = JsonDocument.Parse(
            await wrongCurrency.Content.ReadAsStringAsync());
        Assert.Equal(
            "CURRENCY_MISMATCH",
            currencyProblem.RootElement.GetProperty("code").GetString());

        var missingCampaign = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = Guid.NewGuid(),
                Amount = 10m
            });

        Assert.Equal(HttpStatusCode.NotFound, missingCampaign.StatusCode);

        var valid = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = usdCampaign.Id,
                Amount = 100m
            });

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    [Fact]
    public async Task Allocations_above_the_campaign_goal_are_flagged_for_review()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var donor = await CreateDonorAsync(admin, $"Goal Donor {Guid.NewGuid():N}");
        var campaign = await CreateActiveCampaignAsync(admin, goalAmount: 100m, currency: "USD");
        var donation = await CreateDonationAsync(admin, donor.Id, amount: 500m, currency: "USD");

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = campaign.Id,
                Amount = 150m
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var allocated = await response.Content.ReadFromJsonAsync<DonationDto>(JsonOptions);
        Assert.NotNull(allocated);
        var allocation = Assert.Single(allocated!.Allocations);
        Assert.True(allocation.IsSuspicious);
        Assert.NotNull(allocation.SuspiciousReason);

        var clearResponse = await admin.PatchAsJsonAsync(
            $"/api/v1/donations/allocations/{allocation.Id}/flag",
            new FlagRequestDto { IsSuspicious = false });

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await clearResponse.Content
            .ReadFromJsonAsync<DonationAllocationDto>(JsonOptions);
        Assert.NotNull(cleared);
        Assert.False(cleared!.IsSuspicious);
        Assert.Null(cleared.SuspiciousReason);
    }

    // -----------------------------------------------------------------------
    // Campaign expenditure and the transparency report (FR-DON-04, FR-DON-05)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Expenses_and_report_aggregate_by_currency_without_donor_names()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);

        var currency = UniqueCurrency();
        var donorName = $"Private Donor {Guid.NewGuid():N}";
        var donor = await CreateDonorAsync(admin, donorName);
        var campaign = await CreateActiveCampaignAsync(admin, goalAmount: 1000m, currency: currency);
        var donation = await CreateDonationAsync(admin, donor.Id, amount: 400m, currency: currency);

        var allocationResponse = await admin.PostAsJsonAsync(
            $"/api/v1/donations/{donation.Id}/allocations",
            new CreateDonationAllocationRequestDto
            {
                CampaignId = campaign.Id,
                Amount = 400m
            });
        Assert.Equal(HttpStatusCode.OK, allocationResponse.StatusCode);

        var wrongCurrencyExpense = await admin.PostAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}/expenses",
            new CreateCampaignExpenseRequestDto
            {
                Description = "Fuel",
                Amount = 50m,
                Currency = "USD" == currency ? "GBP" : "USD"
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongCurrencyExpense.StatusCode);

        var expenseResponse = await admin.PostAsJsonAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}/expenses",
            new CreateCampaignExpenseRequestDto
            {
                Description = "Water trucks",
                Amount = 120.505m,
                Currency = currency
            });
        Assert.Equal(HttpStatusCode.Created, expenseResponse.StatusCode);
        var expense = await expenseResponse.Content
            .ReadFromJsonAsync<CampaignExpenseDto>(JsonOptions);
        Assert.NotNull(expense);
        Assert.Equal(120.51m, expense!.Amount);

        var expensesResponse = await admin.GetAsync(
            $"/api/v1/donations/campaigns/{campaign.Id}/expenses");
        Assert.Equal(HttpStatusCode.OK, expensesResponse.StatusCode);
        var expenses = await expensesResponse.Content
            .ReadFromJsonAsync<IReadOnlyCollection<CampaignExpenseDto>>(JsonOptions);
        Assert.NotNull(expenses);
        Assert.Single(expenses!);

        var now = DateTime.UtcNow;
        var from = Uri.EscapeDataString(now.AddHours(-2).ToString("O"));
        var to = Uri.EscapeDataString(now.AddHours(2).ToString("O"));

        var reportResponse = await admin.GetAsync(
            $"/api/v1/donations/report?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var rawReport = await reportResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(donorName, rawReport, StringComparison.OrdinalIgnoreCase);

        var report = JsonSerializer.Deserialize<DonationReportDto>(rawReport, JsonOptions);
        Assert.NotNull(report);

        var totals = Assert.Single(
            report!.CurrencyTotals,
            item => item.Currency == currency);
        Assert.Equal(400m, totals.MonetaryTotal);
        Assert.Equal(0m, totals.MaterialEstimatedTotal);
        Assert.Equal(400m, totals.AllocatedTotal);
        Assert.Equal(120.51m, totals.SpentTotal);
        Assert.Equal(1, totals.DonationCount);
        Assert.Equal(1, totals.DonorCount);

        var campaignLine = Assert.Single(
            report.Campaigns,
            item => item.CampaignId == campaign.Id);
        Assert.Equal(400m, campaignLine.RaisedAmount);
        Assert.Equal(120.51m, campaignLine.SpentAmount);
        Assert.Equal(279.49m, campaignLine.RemainingAmount);
        Assert.Equal(40d, campaignLine.ProgressPercent);
        Assert.Equal(currency, campaignLine.Currency);

        var reversed = await admin.GetAsync(
            $"/api/v1/donations/report?from={to}&to={from}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reversed.StatusCode);
    }

    [Fact]
    public async Task Unknown_ids_return_404_for_every_donation_resource()
    {
        using var admin = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.AdminEmail);
        var missing = Guid.NewGuid();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/donations/donors/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/donations/campaigns/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/donations/{missing}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.GetAsync($"/api/v1/donations/campaigns/{missing}/expenses")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.PatchAsJsonAsync(
                $"/api/v1/donations/{missing}/flag",
                new FlagRequestDto { IsSuspicious = false })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await admin.PatchAsJsonAsync(
                $"/api/v1/donations/allocations/{missing}/flag",
                new FlagRequestDto { IsSuspicious = false })).StatusCode);
    }

    [Fact]
    public async Task Read_only_partner_officers_can_view_donation_data()
    {
        using var officer = await _factory.CreateAuthenticatedClientAsync(
            CmsWebApplicationFactory.OfficerEmail);

        Assert.Equal(
            HttpStatusCode.OK,
            (await officer.GetAsync("/api/v1/donations/donors")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await officer.GetAsync("/api/v1/donations/campaigns")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await officer.GetAsync("/api/v1/donations")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await officer.GetAsync(
                "/api/v1/donations/report?from=2026-01-01&to=2026-12-31")).StatusCode);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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
                Name = "Lifecycle Campaign",
                Description = "Created by the donation integration tests.",
                GoalAmount = goalAmount,
                Currency = currency,
                StartsAt = DateTime.UtcNow.AddDays(-1)
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var campaign = await response.Content.ReadFromJsonAsync<CampaignDto>(JsonOptions)
            ?? throw new InvalidOperationException("Campaign response was empty.");

        Assert.Equal(CampaignStatus.Draft, campaign.Status);

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

    /// <summary>Builds a three-letter currency code so report totals stay isolated.</summary>
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
