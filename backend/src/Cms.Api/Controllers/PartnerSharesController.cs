using Cms.Application.Cases.DTOs;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ICurrentUser = Cms.Application.Contracts.ICurrentUser;

namespace Cms.Api.Controllers;

/// <summary>
/// FR-COORD-06 / FR-PTN-01 partner visibility. The service is composed from
/// already-registered module dependencies here so this DEV2-owned controller
/// does not require an edit to the shared module registration file.
/// </summary>
[ApiController]
[Route("api/v1/partner-shares")]
public sealed class PartnerSharesController : ControllerBase
{
    private readonly PartnerShareService _shares;

    public PartnerSharesController(
        CmsDbContext context,
        ICurrentUser currentUser,
        IDateTimeService dateTimeService,
        IRequestReader requestReader,
        CaseWriteHelper writer)
    {
        _shares = new PartnerShareService(
            context,
            currentUser,
            dateTimeService,
            requestReader,
            writer);
    }

    [HttpPost]
    [Authorize(Roles = CasesRoles.Staff)]
    [ProducesResponseType(typeof(PartnerShareDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Share(
        [FromBody] CreatePartnerShareDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var share = await _shares.ShareAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { requestId = share.RequestId }, share);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet]
    [Authorize(Roles = CasesRoles.Staff + "," + CasesRoles.PartnerOfficer)]
    [ProducesResponseType(typeof(IReadOnlyList<PartnerShareDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _shares.ListAsync(requestId, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = CasesRoles.Staff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _shares.RevokeAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
