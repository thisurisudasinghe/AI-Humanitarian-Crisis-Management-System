using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;
using Cms.Application.Interfaces;
using Cms.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

/// <summary>Identity and access endpoints for M1.</summary>
[ApiController]
[Route("api/v1/auth")]
[Consumes("application/json")]
[Produces("application/json")]
[EnableRateLimiting("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>Registers a new citizen account and returns access tokens.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _authenticationService.RegisterAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Authenticates a user with email and password.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _authenticationService.LoginAsync(request, cancellationToken));
    }

    /// <summary>Rotates a refresh token and issues a new access token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AuthResponseDto>> Refresh(
        [FromBody] RefreshRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _authenticationService.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken));
    }

    /// <summary>Revokes the supplied refresh token.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authenticationService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Requests a password reset email for an account.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(typeof(ForgotPasswordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _authenticationService.ForgotPasswordAsync(request, cancellationToken));
    }

    /// <summary>Resets a password using a single-use reset token.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        await _authenticationService.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Smoke check for the EmergencyCoordinator and CaseManager role pair.</summary>
    [HttpGet("role-check")]
    [Authorize(Roles = RoleNames.EmergencyCoordinator + "," + RoleNames.CaseManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public IActionResult RoleCheck()
    {
        return Ok(new { Authorized = true });
    }

    /// <summary>Redeems a one-time email verification token.</summary>
    [HttpPost("email/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("email-verification")]
    [ProducesResponseType(typeof(EmailVerificationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EmailVerificationResponseDto>> VerifyEmail(
        [FromBody] VerifyEmailRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _authenticationService.VerifyEmailAsync(request, cancellationToken));
    }

    /// <summary>Requests a replacement verification link without revealing account existence.</summary>
    [HttpPost("email/resend")]
    [AllowAnonymous]
    [EnableRateLimiting("email-verification")]
    [ProducesResponseType(typeof(EmailVerificationResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EmailVerificationResponseDto>> ResendVerification(
        [FromBody] ResendVerificationRequestDto request,
        CancellationToken cancellationToken)
    {
        var response = await _authenticationService.ResendVerificationAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status202Accepted, response);
    }

    /// <summary>Returns the authenticated user's profile and roles.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken)
    {
        return Ok(await _authenticationService.GetCurrentUserAsync(cancellationToken));
    }
}
