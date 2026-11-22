using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/households")]
public class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _households;

    public HouseholdsController(IHouseholdService households)
    {
        _households = households;
    }

    [HttpGet("me")]
    [Authorize(Roles = CasesRoles.Citizen)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _households.GetMineAsync(cancellationToken);
            return result is null
                ? CasesProblem.From(new NotFoundException("HOUSEHOLD_NOT_FOUND", "Household profile was not found."))
                : Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPut("me")]
    [Authorize(Roles = CasesRoles.Citizen)]
    public async Task<IActionResult> SaveMine(
        [FromBody] SaveHouseholdDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _households.UpsertMineAsync(request, cancellationToken);
            return Ok(new { id });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
