using Cms.Application.DTOs;
using Cms.Application.DTOs.Donations;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Donor and donation management (M8, lean). Monetary and material donations
/// are recorded manually, allocated to campaigns, tracked against campaign
/// expenditure and summarised in a privacy-safe transparency report.
/// </summary>
[ApiController]
[Route("api/v1/donations")]
[Authorize(Policy = "DonationViewer")]
[Produces("application/json")]
public sealed class DonationsController : ControllerBase
{
    private readonly IDonationService _donationService;

    public DonationsController(IDonationService donationService)
    {
        _donationService = donationService;
    }

    // -----------------------------------------------------------------------
    // Donors (FR-DON-01)
    // -----------------------------------------------------------------------

    /// <summary>Lists donors with optional name/email search and active filter.</summary>
    [HttpGet("donors")]
    [ProducesResponseType(typeof(PagedResultDto<DonorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<DonorDto>>> GetDonors(
        [FromQuery] DonorQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetDonorsAsync(query, cancellationToken));
    }

    /// <summary>Returns a single donor with contribution totals.</summary>
    [HttpGet("donors/{id:guid}")]
    [ProducesResponseType(typeof(DonorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonorDto>> GetDonor(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetDonorAsync(id, cancellationToken));
    }

    /// <summary>Registers a new donor.</summary>
    [HttpPost("donors")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonorDto>> CreateDonor(
        [FromBody] CreateDonorRequestDto request,
        CancellationToken cancellationToken)
    {
        var donor = await _donationService.CreateDonorAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDonor), new { id = donor.Id }, donor);
    }

    /// <summary>Updates a donor. Only supplied fields are changed.</summary>
    [HttpPatch("donors/{id:guid}")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonorDto>> UpdateDonor(
        Guid id,
        [FromBody] UpdateDonorRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.UpdateDonorAsync(id, request, cancellationToken));
    }

    // -----------------------------------------------------------------------
    // Campaigns (FR-DON-03, FR-DON-04)
    // -----------------------------------------------------------------------

    /// <summary>Lists campaigns with optional search and status filter.</summary>
    [HttpGet("campaigns")]
    [ProducesResponseType(typeof(PagedResultDto<CampaignDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<CampaignDto>>> GetCampaigns(
        [FromQuery] CampaignQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetCampaignsAsync(query, cancellationToken));
    }

    /// <summary>Returns a campaign with raised, spent and remaining totals.</summary>
    [HttpGet("campaigns/{id:guid}")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> GetCampaign(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetCampaignAsync(id, cancellationToken));
    }

    /// <summary>Creates a campaign in the Draft state.</summary>
    [HttpPost("campaigns")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> CreateCampaign(
        [FromBody] CreateCampaignRequestDto request,
        CancellationToken cancellationToken)
    {
        var campaign = await _donationService.CreateCampaignAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCampaign), new { id = campaign.Id }, campaign);
    }

    /// <summary>Updates a campaign, including its lifecycle status.</summary>
    [HttpPatch("campaigns/{id:guid}")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(CampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> UpdateCampaign(
        Guid id,
        [FromBody] UpdateCampaignRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.UpdateCampaignAsync(id, request, cancellationToken));
    }

    /// <summary>Lists the expenses recorded against a campaign.</summary>
    [HttpGet("campaigns/{id:guid}/expenses")]
    [ProducesResponseType(
        typeof(IReadOnlyCollection<CampaignExpenseDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<CampaignExpenseDto>>> GetCampaignExpenses(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetCampaignExpensesAsync(id, cancellationToken));
    }

    /// <summary>Records campaign expenditure. Currency must match the campaign.</summary>
    [HttpPost("campaigns/{id:guid}/expenses")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(CampaignExpenseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignExpenseDto>> CreateCampaignExpense(
        Guid id,
        [FromBody] CreateCampaignExpenseRequestDto request,
        CancellationToken cancellationToken)
    {
        var expense = await _donationService.CreateCampaignExpenseAsync(
            id,
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetCampaignExpenses),
            new { id = expense.CampaignId },
            expense);
    }

    // -----------------------------------------------------------------------
    // Donations (FR-DON-02, FR-DON-03, FR-DON-06)
    // -----------------------------------------------------------------------

    /// <summary>Lists donations with filters for donor, campaign, type and review state.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<DonationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<DonationDto>>> GetDonations(
        [FromQuery] DonationQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetDonationsAsync(query, cancellationToken));
    }

    /// <summary>Returns a donation with its allocations.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonationDto>> GetDonation(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetDonationAsync(id, cancellationToken));
    }

    /// <summary>
    /// Records a donation. Values above the configured threshold are flagged for
    /// human review; the donation is still stored with its original status.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonationDto>> CreateDonation(
        [FromBody] CreateDonationRequestDto request,
        CancellationToken cancellationToken)
    {
        var donation = await _donationService.CreateDonationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDonation), new { id = donation.Id }, donation);
    }

    /// <summary>
    /// Allocates part of a donation to an active campaign. Allocations are
    /// capped by the donation value and must match the campaign currency.
    /// </summary>
    [HttpPost("{id:guid}/allocations")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonationDto>> AllocateDonation(
        Guid id,
        [FromBody] CreateDonationAllocationRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.AllocateDonationAsync(id, request, cancellationToken));
    }

    /// <summary>Sets or clears the human review flag on a donation.</summary>
    [HttpPatch("{id:guid}/flag")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonationDto>> SetDonationFlag(
        Guid id,
        [FromBody] FlagRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.SetDonationFlagAsync(id, request, cancellationToken));
    }

    /// <summary>Sets or clears the human review flag on an allocation.</summary>
    [HttpPatch("allocations/{id:guid}/flag")]
    [Authorize(Policy = "DonationManager")]
    [ProducesResponseType(typeof(DonationAllocationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonationAllocationDto>> SetAllocationFlag(
        Guid id,
        [FromBody] FlagRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.SetAllocationFlagAsync(id, request, cancellationToken));
    }

    // -----------------------------------------------------------------------
    // Transparency report (FR-DON-05)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Aggregated donor transparency report. Totals are grouped by currency and
    /// never include donor names, so anonymity is preserved.
    /// </summary>
    [HttpGet("report")]
    [ProducesResponseType(typeof(DonationReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonationReportDto>> GetReport(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        return Ok(await _donationService.GetReportAsync(from, to, cancellationToken));
    }
}
