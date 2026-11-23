using Cms.Application.DTOs;
using Cms.Application.DTOs.Authentication;

namespace Cms.Application.Interfaces;

public interface IAuthenticationService
{
    Task<AuthResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task<CurrentUserDto> GetCurrentUserAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a safe public-participation role to the signed-in account. The
    /// server validates an allowlist so privileged roles can never be granted
    /// through this endpoint.
    /// </summary>
    Task<AuthResponseDto> EnrollPublicRoleAsync(
        string purpose,
        CancellationToken cancellationToken = default);

    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default);
}