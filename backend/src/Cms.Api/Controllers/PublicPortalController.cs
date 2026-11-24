using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Application.DTOs.Donations;
using Cms.Application.DTOs.Portal;
using Cms.Application.Interfaces;
using Cms.Application.Operations;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Caller-scoped read and write endpoints for the shared public user portal.
/// These endpoints never grant privileged roles and never expose another
/// participant's data: every query is restricted to the authenticated user.
/// </summary>
[ApiController]
[Route("api/v1/portal")]
[Authorize(Policy = "PublicPortal")]
[Produces("application/json")]
public sealed class PublicPortalController : ControllerBase
{
    private readonly IAuthenticationService _authentication;
    private readonly IDonationService _donations;
    private readonly IOperationsService _operations;
    private readonly ICurrentUser _currentUser;

    public PublicPortalController(
        IAuthenticationService authentication,
        IDonationService donations,
        IOperationsService operations,
        ICurrentUser currentUser)
    {
        _authentication = authentication;
        _donations = donations;
        _operations = operations;
        _currentUser = currentUser;
    }

    // -----------------------------------------------------------------------
    // Enrolment
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a safe public participation role to the signed-in account. Staff,
    /// coordinator, logistics and administrator roles are rejected server-side.
    /// </summary>
    [HttpPost("enroll")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> Enroll(
        [FromBody] EnrollPublicRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _authentication.EnrollPublicRoleAsync(request.Purpose, cancellationToken));
    }

    // -----------------------------------------------------------------------
    // Volunteer portal
    // -----------------------------------------------------------------------

    /// <summary>Returns the caller's volunteer profile, availability and open opportunities.</summary>
    [HttpGet("volunteer/status")]
    [Authorize(Roles = RoleNames.Volunteer)]
    [ProducesResponseType(typeof(VolunteerPortalDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<VolunteerPortalDto>> VolunteerStatus(
        CancellationToken cancellationToken)
    {
        return Ok(await _operations.GetVolunteerPortalAsync(cancellationToken));
    }

    // -----------------------------------------------------------------------
    // Donor portal
    // -----------------------------------------------------------------------

    /// <summary>Returns the donor profile linked to the signed-in account.</summary>
    [HttpGet("donor/profile")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(DonorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonorDto>> DonorProfile(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var donor = await _donations.GetDonorForUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException(
                "A donor profile is not linked to this account.",
                "DONOR_PROFILE_NOT_FOUND");

        return Ok(donor);
    }

    /// <summary>Lists active campaigns that can receive recorded donations.</summary>
    [HttpGet("donor/campaigns")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(PagedResultDto<CampaignDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<CampaignDto>>> DonorCampaigns(
        [FromQuery] CampaignQueryDto query,
        CancellationToken cancellationToken)
    {
        query.Status = Cms.Domain.Enums.CampaignStatus.Active;
        query.PageSize = Math.Clamp(query.PageSize, 1, 50);
        return Ok(await _donations.GetCampaignsAsync(query, cancellationToken));
    }

    /// <summary>Lists only the signed-in donor's contribution history.</summary>
    [HttpGet("donor/donations")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(PagedResultDto<DonationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<DonationDto>>> DonorDonations(
        [FromQuery] DonationQueryDto query,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        return Ok(await _donations.GetDonationsForUserAsync(userId, query, cancellationToken));
    }

    /// <summary>Returns one donation only when it belongs to the signed-in donor.</summary>
    [HttpGet("donor/donations/{id:guid}")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonationDto>> DonorDonation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        return await _donations.GetDonationForUserAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException(
                "This donation is not available for your account.",
                "DONATION_NOT_FOUND");
    }

    /// <summary>
    /// Records a donation against the caller's donor profile. This uses the
    /// existing supported recording workflow and does not process payments.
    /// </summary>
    [HttpPost("donor/donations")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(DonationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DonationDto>> RecordDonation(
        [FromBody] CreateDonationRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        return Ok(await _donations.CreateDonationForUserAsync(userId, request, cancellationToken));
    }

    /// <summary>Privacy-safe aggregated allocation and expenditure report.</summary>
    [HttpGet("donor/transparency")]
    [Authorize(Roles = RoleNames.Donor)]
    [ProducesResponseType(typeof(DonationReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DonationReportDto>> Transparency(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken)
    {
        return Ok(await _donations.GetReportAsync(from, to, cancellationToken));
    }
}
