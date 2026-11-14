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
}