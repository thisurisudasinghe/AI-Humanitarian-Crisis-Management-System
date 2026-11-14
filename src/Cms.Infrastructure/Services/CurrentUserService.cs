using System.Security.Claims;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Cms.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userId = User?.FindFirst("sub")?.Value
                ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(userId, out var id) ? id : null;
        }
    }

    public string? Email =>
        User?.FindFirst("email")?.Value
        ?? User?.FindFirst(ClaimTypes.Email)?.Value;

    public IReadOnlyCollection<string> Roles =>
        User?.FindAll("role")
            .Select(claim => claim.Value)
            .Distinct()
            .ToArray()
        ?? Array.Empty<string>();

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;
}